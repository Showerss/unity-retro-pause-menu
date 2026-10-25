using Retro.Combat;
using UnityEngine;

namespace Retro.Menu.Adapters
{
    /// <summary>
    /// Drives <see cref="ScreenShake.Strength"/> from the options "Screen shake" slider.
    /// Excluded from compilation unless com.retro.combat is present. Put it next to the SettingsApplier.
    /// </summary>
    public class ScreenShakeSettingsTarget : MonoBehaviour, ISettingsTarget
    {
        void OnEnable()
        {
            MenuRegistry.Register(this);
            ApplySettings();
        }

        void OnDisable() => MenuRegistry.Unregister(this);

        public void ApplySettings() => ScreenShake.Strength = GameSettings.ScreenShake;
    }
}
