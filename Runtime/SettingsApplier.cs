using UnityEngine;

namespace Retro.Menu
{
    /// <summary>
    /// Applies the engine-level part of <see cref="GameSettings"/> - camera FOV and
    /// master volume - then forwards to every registered <see cref="ISettingsTarget"/>
    /// so game-specific integrations can apply their own share.
    ///
    /// Put this on the same object as the PauseMenuController.
    ///
    /// Note on FOV: if a controller script lerps the camera's FOV each frame (a sprint
    /// kick, for example) writing Camera.fieldOfView here is not enough - it will be
    /// overwritten. Such a controller needs an ISettingsTarget adapter that writes the
    /// controller's own baseline field instead.
    /// </summary>
    public class SettingsApplier : MonoBehaviour
    {
        [Tooltip("Leave empty to use Camera.main.")]
        [SerializeField] private Camera _camera;

        Camera Target => _camera != null ? _camera : Camera.main;

        void OnEnable()
        {
            GameSettings.OnChanged += Apply;
            Apply();
        }

        void OnDisable() => GameSettings.OnChanged -= Apply;

        public void Apply()
        {
            Camera cam = Target;
            if (cam != null) cam.fieldOfView = GameSettings.Fov;

            AudioListener.volume = GameSettings.Volume;

            foreach (ISettingsTarget target in MenuRegistry.SettingsTargets)
            {
                try { target.ApplySettings(); }
                catch (System.Exception e)
                {
                    Debug.LogError($"ISettingsTarget threw while applying settings: {e}");
                }
            }
        }
    }
}
