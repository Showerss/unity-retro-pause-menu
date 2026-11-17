using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Retro.Menu.Tests
{
    public class SaveIntegrityTests
    {
        private sealed class Participant : ISaveParticipant
        {
            public string SaveKey { get; set; } = "test";
            public string State = "{\"value\":1}";
            public bool ThrowCapture;
            public bool ThrowRestore;
            public int RestoreCount;

            public string Capture()
            {
                if (ThrowCapture) throw new InvalidOperationException("capture failed");
                return State;
            }

            public void Restore(string json)
            {
                RestoreCount++;
                State = json;
                if (ThrowRestore) throw new InvalidOperationException("restore failed");
            }
        }

        private int _slot;
        private string _path;
        private Participant _participant;
        private readonly List<GameObject> _objects = new List<GameObject>();
        private Func<int, bool> _previousHandler;
        private int _savedCount;
        private int _restoredCount;
        private readonly List<bool> _completions = new List<bool>();
        private readonly List<string> _notificationOrder = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _slot = Guid.NewGuid().GetHashCode() & int.MaxValue;
            _path = Path.Combine(Application.persistentDataPath, $"save_{_slot}.json");
            _previousHandler = SaveSystem.LoadHandler;
            SaveSystem.LoadHandler = null;
            MenuRegistry.Clear();
            _participant = new Participant();
            MenuRegistry.Register(_participant);
            _savedCount = 0;
            _restoredCount = 0;
            _completions.Clear();
            _notificationOrder.Clear();
            SaveSystem.Saved += HandleSaved;
            GameSaveService.Restored += HandleRestored;
            SaveSystem.LoadCompleted += HandleLoadCompleted;
        }

        [TearDown]
        public void TearDown()
        {
            SetRestoring(false);
            foreach (GameObject obj in _objects)
            {
                if (obj.TryGetComponent(out PauseMenuController menu))
                {
                    SetField(menu, "_pendingLoadSlot", null);
                    menu.Resume();
                }
                UnityEngine.Object.DestroyImmediate(obj);
            }
            _objects.Clear();
            SaveSystem.Saved -= HandleSaved;
            GameSaveService.Restored -= HandleRestored;
            SaveSystem.LoadCompleted -= HandleLoadCompleted;
            SaveSystem.LoadHandler = _previousHandler;
            MenuRegistry.Clear();
            File.Delete(_path);
            foreach (string temp in Directory.GetFiles(Application.persistentDataPath, $"save_{_slot}.json.*.tmp"))
                File.Delete(temp);
        }

        private void HandleSaved(int slot) => _savedCount++;
        private void HandleRestored()
        {
            Assert.That(GameSaveService.IsRestoring, Is.False);
            _restoredCount++;
            _notificationOrder.Add("restored");
        }

        private void HandleLoadCompleted(int slot, bool success)
        {
            Assert.That(slot, Is.EqualTo(_slot));
            Assert.That(GameSaveService.IsRestoring, Is.False);
            _completions.Add(success);
            _notificationOrder.Add("completed");
        }

        private T Create<T>() where T : Component
        {
            var obj = new GameObject(typeof(T).Name);
            _objects.Add(obj);
            return obj.AddComponent<T>();
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void SetRestoring(bool value) =>
            typeof(GameSaveService).GetProperty(nameof(GameSaveService.IsRestoring)).SetValue(null, value);

        private bool ApplySlot(GameSaveService service)
        {
            SetField(service, "_loadingSlot", _slot);
            return (bool)typeof(GameSaveService).GetMethod("ApplyLoadedSlot", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(service, null);
        }

        [Test]
        public void ThrowingCapturePreservesPreviousSlotAndDoesNotNotifySaved()
        {
            Assert.That(SaveSystem.Save(_slot), Is.True);
            byte[] previous = File.ReadAllBytes(_path);
            MenuRegistry.Register(new Participant { SaveKey = "broken", ThrowCapture = true });
            LogAssert.Expect(LogType.Error, new Regex("threw while capturing"));

            Assert.That(SaveSystem.Save(_slot), Is.False);
            Assert.That(File.ReadAllBytes(_path), Is.EqualTo(previous));
            Assert.That(_savedCount, Is.EqualTo(1));
        }

        [Test]
        [UnityPlatform(RuntimePlatform.WindowsEditor)]
        public void FailedReplacementPreservesPreviousSlotAndCleansStagingFile()
        {
            Assert.That(SaveSystem.Save(_slot), Is.True);
            byte[] previous = File.ReadAllBytes(_path);
            _participant.State = "{\"value\":2}";
            using (var locked = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                LogAssert.Expect(LogType.Error, new Regex("SaveSystem.Save failed"));
                Assert.That(SaveSystem.Save(_slot), Is.False);
            }

            Assert.That(File.ReadAllBytes(_path), Is.EqualTo(previous));
            Assert.That(Directory.GetFiles(Application.persistentDataPath, $"save_{_slot}.json.*.tmp"), Is.Empty);
            Assert.That(_savedCount, Is.EqualTo(1));
            Assert.That(SaveSystem.Load(_slot), Is.True);
            Assert.That(_participant.State, Is.EqualTo("{\"value\":1}"));
        }

        [Test]
        public void ThrowingRestoreReturnsFailureWithoutPretendingToRollback()
        {
            var next = new Participant { SaveKey = "next" };
            MenuRegistry.Register(next);
            Assert.That(SaveSystem.Save(_slot), Is.True);
            _participant.State = "{\"value\":2}";
            _participant.ThrowRestore = true;
            LogAssert.Expect(LogType.Error, new Regex("threw while restoring"));

            Assert.That(SaveSystem.Load(_slot), Is.False);
            Assert.That(_participant.State, Is.EqualTo("{\"value\":1}"), "Partial mutations are not rolled back.");
            Assert.That(next.RestoreCount, Is.EqualTo(1), "Later participants are still attempted.");
        }

        [Test]
        public void NormalRoundTripReplacesExistingSlotAndPreservesFormat()
        {
            Assert.That(SaveSystem.Save(_slot), Is.True);
            _participant.State = "{\"value\":2}";
            Assert.That(SaveSystem.Save(_slot), Is.True);
            _participant.State = "";

            Assert.That(SaveSystem.Load(_slot), Is.True);
            Assert.That(_participant.State, Is.EqualTo("{\"value\":2}"));
            Assert.That(SaveSystem.Peek(_slot).formatVersion, Is.EqualTo(1));
            Assert.That(_savedCount, Is.EqualTo(2));
            Assert.That(Directory.GetFiles(Application.persistentDataPath, $"save_{_slot}.json.*.tmp"), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ServiceOnlyFiresRestoredAfterSuccessfulRestoration(bool fail)
        {
            Assert.That(SaveSystem.Save(_slot), Is.True);
            GameSaveService service = Create<GameSaveService>();
            _participant.ThrowRestore = fail;
            if (fail) LogAssert.Expect(LogType.Error, new Regex("threw while restoring"));

            Assert.That(ApplySlot(service), Is.EqualTo(!fail));

            Assert.That(GameSaveService.IsRestoring, Is.False);
            Assert.That(_restoredCount, Is.EqualTo(fail ? 0 : 1));
            Assert.That(_completions, Is.EqualTo(new[] { !fail }));
            Assert.That(_notificationOrder, Is.EqualTo(fail
                ? new[] { "completed" }
                : new[] { "restored", "completed" }));
        }

        [Test]
        public void MenuDoesNotTreatAcceptedAsyncRequestAsCompleted()
        {
            PauseMenuController menu = Create<PauseMenuController>();
            SetField(menu, "_lockCursorOnResume", false);
            menu.Pause();
            SaveSystem.LoadHandler = slot => true;

            menu.LoadFromSlot(_slot);

            Assert.That(PauseMenuController.IsPaused, Is.True);
            Assert.That(_completions, Is.Empty);
        }

        [Test]
        public void SavingDuringRestorationCannotReplaceSlotWithUnloadedParticipants()
        {
            Assert.That(SaveSystem.Save(_slot), Is.True);
            byte[] previous = File.ReadAllBytes(_path);
            MenuRegistry.Clear();
            SetRestoring(true);
            LogAssert.Expect(LogType.Warning, "Cannot save while a load is in progress.");

            Assert.That(SaveSystem.Save(_slot), Is.False);

            Assert.That(File.ReadAllBytes(_path), Is.EqualTo(previous));
            Assert.That(_savedCount, Is.EqualTo(1));
            Assert.That(Directory.GetFiles(Application.persistentDataPath, $"save_{_slot}.json.*.tmp"), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PendingMenuLoadPreservesSlotAndReleasesSaveGuardOnCompletion(bool success)
        {
            Assert.That(SaveSystem.Save(_slot), Is.True);
            byte[] previous = File.ReadAllBytes(_path);
            PauseMenuController menu = Create<PauseMenuController>();
            SetField(menu, "_lockCursorOnResume", false);
            menu.Pause();
            SaveSystem.LoadHandler = slot => true;
            menu.LoadFromSlot(_slot);
            MenuRegistry.Clear();
            LogAssert.Expect(LogType.Warning, "Cannot save while a load is in progress.");

            menu.SaveToSlot(_slot);

            Assert.That(File.ReadAllBytes(_path), Is.EqualTo(previous));
            Assert.That(_savedCount, Is.EqualTo(1));
            Assert.That(Directory.GetFiles(Application.persistentDataPath, $"save_{_slot}.json.*.tmp"), Is.Empty);

            MenuRegistry.Register(_participant);
            if (!success)
                LogAssert.Expect(LogType.Warning, new Regex("could not be fully restored.*partially changed"));
            SaveSystem.NotifyLoadCompleted(_slot, success);
            _participant.State = "{\"value\":2}";
            menu.SaveToSlot(_slot);
            Assert.That(_savedCount, Is.EqualTo(2));
            Assert.That(SaveSystem.Peek(_slot).Find(_participant.SaveKey), Is.EqualTo(_participant.State));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PendingMenuLoadBlocksConflictingActionsUntilCompletion(bool success)
        {
            PauseMenuController menu = Create<PauseMenuController>();
            SetField(menu, "_lockCursorOnResume", false);
            var panels = new[] { new GameObject("Main"), new GameObject("Options"),
                new GameObject("Save"), new GameObject("Load") };
            _objects.AddRange(panels);
            SetField(menu, "_mainPanel", panels[0]);
            SetField(menu, "_optionsPanel", panels[1]);
            SetField(menu, "_savePanel", panels[2]);
            SetField(menu, "_loadPanel", panels[3]);
            menu.Pause();
            menu.ShowLoad();
            int requests = 0;
            SaveSystem.LoadHandler = slot => { requests++; return true; };
            menu.LoadFromSlot(_slot);

            menu.ShowSave();
            menu.ShowOptions();
            menu.ShowMain();
            menu.Resume();
            menu.QuitGame();
            menu.LoadFromSlot(_slot + 1);

            Assert.That(panels[0].activeSelf, Is.False);
            Assert.That(panels[1].activeSelf, Is.False);
            Assert.That(panels[2].activeSelf, Is.False);
            Assert.That(panels[3].activeSelf, Is.True);
            Assert.That(PauseMenuController.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(requests, Is.EqualTo(1));

            if (!success)
                LogAssert.Expect(LogType.Warning, new Regex("could not be fully restored.*partially changed"));
            SaveSystem.NotifyLoadCompleted(_slot, success);
            Assert.That(PauseMenuController.IsPaused, Is.EqualTo(!success));
            menu.ShowSave();
            Assert.That(panels[2].activeSelf, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MenuHandlesDeferredServiceCompletionTruthfully(bool fail)
        {
            Assert.That(SaveSystem.Save(_slot), Is.True);
            GameSaveService service = Create<GameSaveService>();
            PauseMenuController menu = Create<PauseMenuController>();
            SetField(menu, "_lockCursorOnResume", false);
            menu.Pause();
            // Hold the async request at its completion boundary, without unloading the test runner's scene.
            SaveSystem.LoadHandler = slot => true;
            menu.LoadFromSlot(_slot);
            Assert.That(PauseMenuController.IsPaused, Is.True);
            _participant.ThrowRestore = fail;
            SetRestoring(true);
            if (fail)
            {
                LogAssert.Expect(LogType.Error, new Regex("threw while restoring"));
                LogAssert.Expect(LogType.Warning, new Regex("could not be fully restored.*partially changed"));
            }

            Assert.That(ApplySlot(service), Is.EqualTo(!fail));

            Assert.That(PauseMenuController.IsPaused, Is.EqualTo(fail));
            Assert.That(_restoredCount, Is.EqualTo(fail ? 0 : 1));
            Assert.That(_completions, Is.EqualTo(new[] { !fail }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MenuHandlesSynchronousCompletionExactlyOnce(bool fail)
        {
            Assert.That(SaveSystem.Save(_slot), Is.True);
            PauseMenuController menu = Create<PauseMenuController>();
            SetField(menu, "_lockCursorOnResume", false);
            menu.Pause();
            _participant.ThrowRestore = fail;
            if (fail)
            {
                LogAssert.Expect(LogType.Error, new Regex("threw while restoring"));
                LogAssert.Expect(LogType.Warning, new Regex("could not be fully restored.*partially changed"));
            }

            menu.LoadFromSlot(_slot);

            Assert.That(PauseMenuController.IsPaused, Is.EqualTo(fail));
            Assert.That(_completions, Is.EqualTo(new[] { !fail }));
        }

        [Test]
        public void RejectedRequestWarnsAndAllowsRetry()
        {
            PauseMenuController menu = Create<PauseMenuController>();
            SetField(menu, "_lockCursorOnResume", false);
            menu.Pause();
            SaveSystem.LoadHandler = slot => false;
            LogAssert.Expect(LogType.Warning, new Regex("Could not start loading save slot"));

            menu.LoadFromSlot(_slot);

            Assert.That(PauseMenuController.IsPaused, Is.True);
            Assert.That(_completions, Is.Empty);
            Assert.That(SaveSystem.Save(_slot), Is.True);
            SaveSystem.LoadHandler = null;
            menu.LoadFromSlot(_slot);
            Assert.That(PauseMenuController.IsPaused, Is.False);
            Assert.That(_completions, Is.EqualTo(new[] { true }));
        }
    }
}
