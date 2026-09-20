using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Retro.Menu.Adapters
{
    /// <summary>
    /// Bridges the pause menu to com.firstperson.controller.
    ///
    /// This whole assembly is excluded from compilation unless that package is present
    /// (see the defineConstraints in the asmdef), so the core menu package still imports
    /// cleanly into a project that has no first-person controller.
    ///
    /// Put this on your player, alongside FirstPersonController.
    ///
    /// Two jobs:
    ///
    /// 1. Pause. The controller's look code rotates by (input * sensitivity) with no
    ///    Time.deltaTime factor, and Update still runs at timeScale 0 - so freezing time
    ///    would leave the camera steerable while paused. We switch the component off.
    ///    PlayerInput goes off too, so re-enabling re-reads device state and the stale
    ///    look input from the moment of pausing is not applied on the first frame back.
    ///
    /// 2. Settings. The controller lerps camera FOV toward its own private baseline each
    ///    frame, so writing Camera.fieldOfView is futile - the baseline is what must
    ///    change. Those fields are private in a package we do not own, so we reach them
    ///    by reflection. If that package ever exposes public setters, replace the two
    ///    TryWriteFloat calls below and delete the helper.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public class FirstPersonControllerAdapter : MonoBehaviour, IPauseTarget, ISettingsTarget
    {
        [Tooltip("Optional. Found on this object if left empty.")]
        [SerializeField] private PlayerInput _playerInput;

        FirstPersonController _controller;

        // Private fields on FirstPersonController that own the values our sliders drive.
        const string DefaultFovField  = "_defaultFov";
        const string SensitivityField = "_sensitivity";

        void Awake()
        {
            _controller = GetComponent<FirstPersonController>();
            if (_playerInput == null) _playerInput = GetComponent<PlayerInput>();
        }

        void OnEnable()  => MenuRegistry.Register(this);
        void OnDisable() => MenuRegistry.Unregister(this);

        public void OnPauseChanged(bool paused)
        {
            if (_controller != null) _controller.enabled = !paused;
            if (_playerInput != null) _playerInput.enabled = !paused;
        }

        public void ApplySettings()
        {
            if (_controller == null) return;
            TryWriteFloat(DefaultFovField, GameSettings.Fov);
            TryWriteFloat(SensitivityField, GameSettings.Sensitivity);
        }

        void TryWriteFloat(string fieldName, float value)
        {
            FieldInfo field = typeof(FirstPersonController)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null || field.FieldType != typeof(float))
            {
                Debug.LogWarning(
                    $"{nameof(FirstPersonControllerAdapter)}: FirstPersonController.{fieldName} not found. " +
                    "The controller package changed shape - update this adapter.", this);
                return;
            }

            field.SetValue(_controller, value);
        }
    }
}
