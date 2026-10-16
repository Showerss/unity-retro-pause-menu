using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Retro.Menu
{
    /// <summary>
    /// Scene-aware saving on top of <see cref="SaveSystem"/>, for games with a persistent Boot scene that loads
    /// gameplay scenes additively. Loading reloads the saved scene and restores it before anything in it starts,
    /// so taken pickups stay gone and one-time events don't replay. Autosaves wait until the game isn't paused
    /// and nothing has blocked them (see <see cref="AddAutosaveBlocker"/>). Put it on a persistent object in Boot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameSaveService : MonoBehaviour
    {
        // Compared by path, because a scene the editor loads for play mode reports build index -1,
        // and this object can't use its own scene once it has been moved to DontDestroyOnLoad.
        [Tooltip("Build index of the persistent Boot scene. It stays loaded and is never saved as the current scene.")]
        [Min(0)]
        [SerializeField] private int _bootSceneBuildIndex;

        [Tooltip("Untick to ignore autosave requests, e.g. for a game with manual saves only.")]
        [SerializeField] private bool _autosaveEnabled = true;

        [Tooltip("Load by reloading the saved scene first (recommended). Untick to restore into the scene as it is.")]
        [SerializeField] private bool _reloadSceneOnLoad = true;

        private static readonly List<Func<bool>> _autosaveBlockers = new List<Func<bool>>();

        private bool _autosavePending;
        private int _loadingSlot;

        /// <summary>True while a save is being applied. Use it to stop scripted events starting mid-load.</summary>
        public static bool IsRestoring { get; private set; }

        /// <summary>Raised once a loaded save has been fully applied.</summary>
        public static event Action Restored;

        /// <summary>Raised after an autosave is written, e.g. to show a "Saving..." note.</summary>
        public static event Action Autosaved;

        private static GameSaveService _instance;

        /// <summary>Asks for an autosave as soon as it's safe. Calling it several times in a row saves once.</summary>
        public static void RequestAutosave()
        {
            if (_instance != null && _instance._autosaveEnabled && !IsRestoring) _instance._autosavePending = true;
        }

        /// <summary>
        /// Holds autosaves back while <paramref name="isBlocking"/> returns true, e.g. while a cutscene plays.
        /// Cleared each time play mode or the game starts.
        /// </summary>
        public static void AddAutosaveBlocker(Func<bool> isBlocking)
        {
            if (isBlocking != null && !_autosaveBlockers.Contains(isBlocking)) _autosaveBlockers.Add(isBlocking);
        }

        public static void RemoveAutosaveBlocker(Func<bool> isBlocking) => _autosaveBlockers.Remove(isBlocking);

        private void OnEnable()
        {
            _instance = this;
            SaveSystem.LoadHandler = Load;
        }

        private void OnDisable()
        {
            if (_instance == this) _instance = null;
            if (SaveSystem.LoadHandler == (Func<int, bool>)Load) SaveSystem.LoadHandler = null;
        }

        private void Update()
        {
            if (!_autosavePending || !CanAutosaveNow()) return;

            _autosavePending = false;
            if (SaveSystem.Save(SaveSystem.AutosaveSlot)) Autosaved?.Invoke();
        }

        private bool CanAutosaveNow()
        {
            if (IsRestoring || PauseMenuController.IsPaused) return false;
            foreach (Func<bool> isBlocking in _autosaveBlockers)
            {
                if (isBlocking()) return false;
            }

            // Only save once a gameplay scene is the active one, never Boot on its own.
            Scene active = SceneManager.GetActiveScene();
            return active.IsValid() && active.isLoaded && !IsBoot(active);
        }

        private bool IsBoot(Scene scene) => scene.path == SceneUtility.GetScenePathByBuildIndex(_bootSceneBuildIndex);

        private bool Load(int slot)
        {
            SaveData data = SaveSystem.Peek(slot);
            if (data == null || IsRestoring) return false;
            if (!Application.CanStreamedLevelBeLoaded(data.sceneName))
            {
                Debug.LogError($"[GameSaveService] Save is for scene '{data.sceneName}', which isn't in Build Settings.");
                return false;
            }

            if (_reloadSceneOnLoad)
            {
                StartCoroutine(ReloadAndRestore(slot, data.sceneName));
                return true;
            }

            _loadingSlot = slot;
            IsRestoring = true;
            ApplyLoadedSlot();
            return true;
        }

        private IEnumerator ReloadAndRestore(int slot, string sceneName)
        {
            IsRestoring = true;
            _autosavePending = false;
            _loadingSlot = slot;

            // Unload every gameplay scene; Boot stays, so there is always a scene left loaded.
            var gameplayScenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && !IsBoot(scene)) gameplayScenes.Add(scene);
            }
            foreach (Scene scene in gameplayScenes)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null) yield return unload;
            }

            // Restore from sceneLoaded: the new scene has run Awake and OnEnable (so its participants are registered)
            // but nothing in it has run Start yet.
            SceneManager.sceneLoaded += HandleSceneLoaded;
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.SetActiveScene(scene);
            ApplyLoadedSlot();
        }

        private void ApplyLoadedSlot()
        {
            _autosavePending = false;
            try
            {
                SaveSystem.Load(_loadingSlot);
            }
            finally
            {
                IsRestoring = false;
            }
            Restored?.Invoke();
        }

        // Static state survives play mode when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsRestoring = false;
            Restored = null;
            Autosaved = null;
            _instance = null;
            _autosaveBlockers.Clear();
        }
    }
}
