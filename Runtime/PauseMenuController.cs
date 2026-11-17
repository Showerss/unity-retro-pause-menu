using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Retro.Menu
{
    /// <summary>
    /// Esc-driven pause menu: Continue / Save / Load / Options / Quit.
    ///
    /// This class deliberately knows nothing about player controllers, inventories or
    /// render features. It freezes time, shows UI, and tells everything registered in
    /// <see cref="MenuRegistry"/> that the pause state changed. Game-specific freezing
    /// lives in adapters - see the Adapters folder.
    ///
    /// Why pausing cannot rely on Time.timeScale alone: a look script that rotates by
    /// (input * sensitivity) without a Time.deltaTime factor keeps working at timeScale
    /// 0, because Update still runs. Freezing time is necessary but not sufficient, so
    /// IPauseTarget implementations are expected to actually switch themselves off.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        public static bool IsPaused { get; private set; }

        /// <summary>Raised with the new paused state, after targets are notified.</summary>
        public static event Action<bool> OnPauseChanged;

        [Header("Panels")]
        [SerializeField] private GameObject _root;
        [SerializeField] private GameObject _mainPanel;
        [SerializeField] private GameObject _optionsPanel;
        [SerializeField] private GameObject _savePanel;
        [SerializeField] private GameObject _loadPanel;

        [Header("Behaviour")]
        [Tooltip("Silence the game while paused.")]
        [SerializeField] private bool _muteWhilePaused = true;

        [Tooltip("Lock and hide the cursor when gameplay resumes.")]
        [SerializeField] private bool _lockCursorOnResume = true;

        float _timeScaleBeforePause = 1f;
        int? _pendingLoadSlot;

        bool IsLoadInProgress => _pendingLoadSlot.HasValue || GameSaveService.IsRestoring;

        void Awake()
        {
            // Static state survives domain reloads in the Editor; never inherit a stale value.
            IsPaused = false;
            ShowOnly(null);
            if (_root != null) _root.SetActive(false);
        }

        void OnDestroy()
        {
            SaveSystem.LoadCompleted -= HandleLoadCompleted;
            // If we are torn down mid-pause, don't leave the game frozen forever.
            if (IsPaused) RestoreTime();
        }

        void Update()
        {
            if (IsLoadInProgress) return;

            // Read the keyboard directly rather than through a PlayerInput action:
            // adapters typically disable PlayerInput while paused, so an Esc binding
            // routed through it could never un-pause us.
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (!IsPaused) Pause();
                else if (_mainPanel != null && _mainPanel.activeSelf) Resume();
                else ShowOnly(_mainPanel); // back out of a sub-panel first
            }
        }

        public void Pause()
        {
            if (IsPaused || IsLoadInProgress) return;
            IsPaused = true;

            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            if (_muteWhilePaused) AudioListener.pause = true;

            NotifyTargets(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_root != null) _root.SetActive(true);
            ShowOnly(_mainPanel);

            OnPauseChanged?.Invoke(true);
        }

        public void Resume()
        {
            if (!IsPaused || IsLoadInProgress) return;
            IsPaused = false;

            RestoreTime();
            NotifyTargets(false);

            if (_lockCursorOnResume)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (_root != null) _root.SetActive(false);

            OnPauseChanged?.Invoke(false);
        }

        static void NotifyTargets(bool paused)
        {
            // Iterate a copy: a target may unregister itself in response.
            var targets = new IPauseTarget[MenuRegistry.PauseTargets.Count];
            for (int i = 0; i < targets.Length; i++) targets[i] = MenuRegistry.PauseTargets[i];

            foreach (IPauseTarget target in targets)
            {
                try { target.OnPauseChanged(paused); }
                catch (Exception e) { Debug.LogError($"IPauseTarget threw on pause change: {e}"); }
            }
        }

        void RestoreTime()
        {
            Time.timeScale = _timeScaleBeforePause <= 0f ? 1f : _timeScaleBeforePause;
            AudioListener.pause = false;
        }

        // ---- Panel routing (wired to the buttons) ----

        public void ShowMain()    => ShowOnly(_mainPanel);
        public void ShowOptions() => ShowOnly(_optionsPanel);
        public void ShowSave()    => ShowOnly(_savePanel);
        public void ShowLoad()    => ShowOnly(_loadPanel);

        void ShowOnly(GameObject panel)
        {
            if (IsLoadInProgress) return;

            if (_mainPanel    != null) _mainPanel.SetActive(panel == _mainPanel);
            if (_optionsPanel != null) _optionsPanel.SetActive(panel == _optionsPanel);
            if (_savePanel    != null) _savePanel.SetActive(panel == _savePanel);
            if (_loadPanel    != null) _loadPanel.SetActive(panel == _loadPanel);
        }

        // ---- Save / Load / Quit ----

        public void SaveToSlot(int slot)
        {
            if (IsLoadInProgress)
            {
                Debug.LogWarning("Cannot save while a load is in progress.");
                return;
            }

            if (SaveSystem.Save(slot)) ShowOnly(_mainPanel);
        }

        public void LoadFromSlot(int slot)
        {
            if (IsLoadInProgress) return;
            _pendingLoadSlot = slot;
            SaveSystem.LoadCompleted += HandleLoadCompleted;
            if (!SaveSystem.RequestLoad(slot) && _pendingLoadSlot.HasValue)
            {
                SaveSystem.LoadCompleted -= HandleLoadCompleted;
                _pendingLoadSlot = null;
                Debug.LogWarning($"Could not start loading save slot {slot}.");
            }
        }

        void HandleLoadCompleted(int slot, bool success)
        {
            if (_pendingLoadSlot != slot) return;
            SaveSystem.LoadCompleted -= HandleLoadCompleted;
            _pendingLoadSlot = null;
            if (success) Resume();
            else Debug.LogWarning($"Save slot {slot} could not be fully restored. The world may be partially changed.");
        }

        public void QuitGame()
        {
            if (IsLoadInProgress) return;

            RestoreTime();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
