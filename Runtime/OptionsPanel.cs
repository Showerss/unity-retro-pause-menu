using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Retro.Menu
{
    /// <summary>
    /// Binds the options sliders to <see cref="GameSettings"/>. The applying itself
    /// happens in <see cref="SettingsApplier"/>, which listens for the change event.
    /// </summary>
    public class OptionsPanel : MonoBehaviour
    {
        [Header("Comfort")]
        [SerializeField] private Slider _fovSlider;
        [SerializeField] private TextMeshProUGUI _fovValue;
        [Tooltip("Lowest and highest field of view the slider offers. Raise the minimum for players prone to motion sickness.")]
        [SerializeField] private Vector2 _fovRange = new Vector2(50f, 100f);
        [SerializeField] private Slider _sensitivitySlider;
        [SerializeField] private TextMeshProUGUI _sensitivityValue;

        [Header("Retro Look")]
        [SerializeField] private Slider _pixelSlider;
        [SerializeField] private TextMeshProUGUI _pixelValue;
        [SerializeField] private Slider _scanlineSlider;
        [SerializeField] private TextMeshProUGUI _scanlineValue;
        [SerializeField] private Slider _vignetteSlider;
        [SerializeField] private TextMeshProUGUI _vignetteValue;

        [Header("Audio")]
        [SerializeField] private Slider _volumeSlider;
        [SerializeField] private TextMeshProUGUI _volumeValue;

        bool _suppressCallbacks;

        void OnEnable()
        {
            PushValuesToWidgets();
            Subscribe(true);
        }

        void OnDisable() => Subscribe(false);

        void Subscribe(bool on)
        {
            Bind(_fovSlider,         v => GameSettings.Fov = v,               on);
            Bind(_sensitivitySlider, v => GameSettings.Sensitivity = v,       on);
            Bind(_pixelSlider,       v => GameSettings.PixelHeight = (int)v,  on);
            Bind(_scanlineSlider,    v => GameSettings.ScanlineIntensity = v, on);
            Bind(_vignetteSlider,    v => GameSettings.VignetteIntensity = v, on);
            Bind(_volumeSlider,      v => GameSettings.Volume = v,            on);
        }

        void Bind(Slider slider, UnityEngine.Events.UnityAction<float> action, bool on)
        {
            if (slider == null) return;

            if (on)
                slider.onValueChanged.AddListener(v => { if (!_suppressCallbacks) { action(v); RefreshLabels(); } });
            else
                slider.onValueChanged.RemoveAllListeners();
        }

        void PushValuesToWidgets()
        {
            _suppressCallbacks = true;

            // A stored value outside the allowed range (e.g. from before the minimum was raised) is pulled into it.
            if (GameSettings.Fov < _fovRange.x || GameSettings.Fov > _fovRange.y)
                GameSettings.Fov = Mathf.Clamp(GameSettings.Fov, _fovRange.x, _fovRange.y);
            Configure(_fovSlider,         _fovRange.x, _fovRange.y, GameSettings.Fov,               false);
            Configure(_sensitivitySlider, 0.02f, 0.6f, GameSettings.Sensitivity,       false);
            Configure(_pixelSlider,       120f, 1080f, GameSettings.PixelHeight,       true);
            Configure(_scanlineSlider,    0f,   1f,    GameSettings.ScanlineIntensity, false);
            Configure(_vignetteSlider,    0f,   1f,    GameSettings.VignetteIntensity, false);
            Configure(_volumeSlider,      0f,   1f,    GameSettings.Volume,            false);

            _suppressCallbacks = false;
            RefreshLabels();
        }

        static void Configure(Slider slider, float min, float max, float value, bool wholeNumbers)
        {
            if (slider == null) return;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.SetValueWithoutNotify(value);
        }

        void RefreshLabels()
        {
            SetLabel(_fovValue,         $"{GameSettings.Fov:0}");
            SetLabel(_sensitivityValue, $"{GameSettings.Sensitivity:0.00}");
            SetLabel(_pixelValue,       $"{GameSettings.PixelHeight}p");
            SetLabel(_scanlineValue,    $"{GameSettings.ScanlineIntensity * 100f:0}%");
            SetLabel(_vignetteValue,    $"{GameSettings.VignetteIntensity * 100f:0}%");
            SetLabel(_volumeValue,      $"{GameSettings.Volume * 100f:0}%");
        }

        static void SetLabel(TextMeshProUGUI label, string text)
        {
            if (label != null) label.text = text;
        }

        /// <summary>Hooked to the Reset button.</summary>
        public void ResetToDefaults()
        {
            GameSettings.ResetToDefaults();
            PushValuesToWidgets();
        }
    }
}
