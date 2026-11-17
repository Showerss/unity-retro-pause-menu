using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Retro.Menu
{
    [Serializable]
    public class SaveEntry
    {
        public string key;
        public string json;
    }

    [Serializable]
    public class SaveData
    {
        public int formatVersion = 1;
        public string savedAtUtc;
        public string sceneName;
        public List<SaveEntry> entries = new List<SaveEntry>();

        public string Find(string key)
        {
            foreach (SaveEntry e in entries)
                if (e.key == key) return e.json;
            return null;
        }

        public string DisplayLabel
        {
            get
            {
                if (!DateTime.TryParse(savedAtUtc, out DateTime parsed)) return sceneName;
                return $"{sceneName}  -  {parsed.ToLocalTime():yyyy-MM-dd HH:mm}";
            }
        }
    }

    /// <summary>
    /// Slot-based JSON saves in Application.persistentDataPath.
    ///
    /// The file is a bag of keyed blobs, one per <see cref="ISaveParticipant"/>. Nothing
    /// here knows what a player or an inventory is - that keeps the package importable
    /// and means adding a new saved system is a new participant, not a schema edit.
    ///
    /// Forward/backward compatibility: unknown keys on load are ignored, and missing
    /// keys simply leave that system untouched. So an old save loads into a newer build
    /// and a new save loads into an older one, both degrading rather than failing.
    ///
    /// Scope: participants decide what they persist. World state - opened doors, already
    /// collected pickups - is NOT covered unless you write a participant for it.
    /// </summary>
    public static class SaveSystem
    {
        public const int SlotCount = 3;

        /// <summary>Slot written by automatic saves, kept apart from the player's manual slots.</summary>
        public const int AutosaveSlot = -1;

        /// <summary>
        /// Optional replacement for <see cref="Load"/> when the menu asks to load, e.g. a game that
        /// reloads its scene first so destroyed pickups and one-shot events come back before restoring.
        /// Return true if the load is under way, then call <see cref="NotifyLoadCompleted"/> when it finishes.
        /// Return false if the request is rejected. Leave null to restore in place.
        /// </summary>
        public static Func<int, bool> LoadHandler { get; set; }

        /// <summary>Raised with the slot after a save is written to disk.</summary>
        public static event Action<int> Saved;

        /// <summary>Raised with the slot and restoration result when a load request finishes.</summary>
        public static event Action<int, bool> LoadCompleted;

        static string PathForSlot(int slot) =>
            Path.Combine(Application.persistentDataPath, slot == AutosaveSlot ? "autosave.json" : $"save_{slot}.json");

        // Static state survives play mode when domain reload is off; never inherit a dead game's hooks.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            LoadHandler = null;
            Saved = null;
            LoadCompleted = null;
        }

        public static bool SlotExists(int slot) => File.Exists(PathForSlot(slot));

        public static SaveData Peek(int slot)
        {
            if (!SlotExists(slot)) return null;
            try
            {
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(PathForSlot(slot)));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Save slot {slot} is unreadable: {e.Message}");
                return null;
            }
        }

        public static bool Save(int slot)
        {
            if (GameSaveService.IsRestoring)
            {
                Debug.LogWarning("Cannot save while a load is in progress.");
                return false;
            }

            var data = new SaveData
            {
                savedAtUtc = DateTime.UtcNow.ToString("o"),
                sceneName  = SceneManager.GetActiveScene().name,
            };

            foreach (ISaveParticipant participant in MenuRegistry.SaveParticipants)
            {
                string key = "<unknown>";
                try
                {
                    key = participant.SaveKey;
                    data.entries.Add(new SaveEntry
                    {
                        key  = key,
                        json = participant.Capture(),
                    });
                }
                catch (Exception e)
                {
                    Debug.LogError($"Save participant '{key}' threw while capturing: {e}");
                    return false;
                }
            }

            if (data.entries.Count == 0)
                Debug.LogWarning("Saving with no registered participants - the file will be empty. " +
                                 "Add a PlayerTransformSaveParticipant to your player, at minimum.");

            string temporaryPath = null;
            bool ownsTemporaryFile = false;
            try
            {
                string json = JsonUtility.ToJson(data, true);
                string destination = PathForSlot(slot);
                temporaryPath = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    ownsTemporaryFile = true;
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write(json);
                        writer.Flush();
                        stream.Flush(true);
                    }
                }

                if (File.Exists(destination)) File.Replace(temporaryPath, destination, null);
                else File.Move(temporaryPath, destination);
                ownsTemporaryFile = false;
                Debug.Log($"Saved slot {slot} -> {PathForSlot(slot)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveSystem.Save failed: {e.Message}");
                return false;
            }
            finally
            {
                if (ownsTemporaryFile)
                {
                    try { File.Delete(temporaryPath); }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"SaveSystem could not remove temporary file '{temporaryPath}': {e.Message}");
                    }
                }
            }

            Saved?.Invoke(slot);
            return true;
        }

        /// <summary>
        /// Loads through <see cref="LoadHandler"/> if set, otherwise restores in place.
        /// True can mean an asynchronous request was accepted, not that restoration succeeded.
        /// Listen to <see cref="LoadCompleted"/> for the final result.
        /// </summary>
        public static bool RequestLoad(int slot)
        {
            if (LoadHandler != null) return LoadHandler(slot);
            bool success = Load(slot);
            NotifyLoadCompleted(slot, success);
            return success;
        }

        /// <summary>Called exactly once by a load handler after restoration finishes, including partial failure.</summary>
        public static void NotifyLoadCompleted(int slot, bool success) => LoadCompleted?.Invoke(slot, success);

        /// <summary>Restores every registered participant from the slot, in place, without changing scenes.</summary>
        public static bool Load(int slot)
        {
            SaveData data = Peek(slot);
            if (data == null) return false;

            bool success = true;
            foreach (ISaveParticipant participant in MenuRegistry.SaveParticipants)
            {
                string key = "<unknown>";
                try
                {
                    key = participant.SaveKey;
                    string json = data.Find(key);
                    if (json == null) continue; // not in this save; leave the system alone
                    participant.Restore(json);
                }
                catch (Exception e)
                {
                    success = false;
                    Debug.LogError($"Save participant '{key}' threw while restoring: {e}");
                }
            }

            return success;
        }

        public static void Delete(int slot)
        {
            if (SlotExists(slot)) File.Delete(PathForSlot(slot));
        }
    }
}
