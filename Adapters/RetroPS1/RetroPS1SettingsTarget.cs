using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Retro.Menu.Adapters
{
    /// <summary>
    /// Drives the retro post-process feature from the options sliders.
    ///
    /// Excluded from compilation unless com.retro.ps1postprocess is present.
    ///
    /// Important: the feature's settings live on a UniversalRendererData asset, which is
    /// a ScriptableObject. Writing them in the Editor dirties that asset on disk and the
    /// change SURVIVES exiting play mode - so a play session spent dragging sliders would
    /// silently overwrite your authored defaults. We snapshot on enable and restore on
    /// disable to prevent exactly that. In a player build the mutation is per-session and
    /// harmless, but the snapshot costs nothing so it runs everywhere.
    /// </summary>
    public class RetroPS1SettingsTarget : MonoBehaviour, ISettingsTarget
    {
        [Tooltip("The UniversalRendererData holding RetroPS1RenderFeature (e.g. PC_Renderer.asset).")]
        [SerializeField] private UniversalRendererData _rendererData;

        RetroPS1RenderFeature _feature;

        bool _snapshotTaken;
        Vector2Int _authoredResolution;
        float _authoredScanlines;
        float _authoredVignette;
        bool _authoredScanlinesEnabled;

        void OnEnable()
        {
            CacheFeature();
            TakeSnapshot();
            MenuRegistry.Register(this);
            ApplySettings();
        }

        void OnDisable()
        {
            MenuRegistry.Unregister(this);
            RestoreSnapshot();
        }

        void CacheFeature()
        {
            if (_feature != null) return;

            if (_rendererData == null)
            {
                Debug.LogWarning($"{nameof(RetroPS1SettingsTarget)}: no UniversalRendererData assigned.", this);
                return;
            }

            foreach (ScriptableRendererFeature feature in _rendererData.rendererFeatures)
            {
                if (feature is RetroPS1RenderFeature retro)
                {
                    _feature = retro;
                    return;
                }
            }

            Debug.LogWarning($"{nameof(RetroPS1SettingsTarget)}: no RetroPS1RenderFeature on " +
                             $"{_rendererData.name}. The retro sliders will do nothing.", this);
        }

        void TakeSnapshot()
        {
            if (_snapshotTaken || _feature == null) return;

            _authoredResolution       = _feature.settings.targetResolution;
            _authoredScanlines        = _feature.settings.scanlineIntensity;
            _authoredVignette         = _feature.settings.vignetteIntensity;
            _authoredScanlinesEnabled = _feature.settings.enableScanlines;
            _snapshotTaken = true;
        }

        void RestoreSnapshot()
        {
            if (!_snapshotTaken || _feature == null) return;

            _feature.settings.targetResolution  = _authoredResolution;
            _feature.settings.scanlineIntensity = _authoredScanlines;
            _feature.settings.vignetteIntensity = _authoredVignette;
            _feature.settings.enableScanlines   = _authoredScanlinesEnabled;
            _snapshotTaken = false;
        }

        public void ApplySettings()
        {
            if (_feature == null) return;

            // Preserve the authored aspect so the slider only changes chunkiness.
            float aspect = _authoredResolution.y == 0
                ? 4f / 3f
                : (float)_authoredResolution.x / _authoredResolution.y;

            int height = GameSettings.PixelHeight;
            _feature.settings.targetResolution  = new Vector2Int(Mathf.RoundToInt(height * aspect), height);
            _feature.settings.scanlineCount     = height;
            _feature.settings.scanlineIntensity = GameSettings.ScanlineIntensity;
            _feature.settings.enableScanlines   = GameSettings.ScanlineIntensity > 0.001f;
            _feature.settings.vignetteIntensity = GameSettings.VignetteIntensity;
        }
    }
}
