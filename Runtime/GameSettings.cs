using System;
using UnityEngine;

namespace Retro.Menu
{
    /// <summary>
    /// Player-facing settings, persisted to PlayerPrefs. Pure data + notification:
    /// nothing here touches the scene. <see cref="SettingsApplier"/> does the applying.
    /// </summary>
    public static class GameSettings
    {
        public static event Action OnChanged;

        const string FovKey         = "retro.fov";
        const string SensitivityKey = "retro.sensitivity";
        const string PixelHeightKey = "retro.pixelHeight";
        const string ScanlineKey    = "retro.scanlines";
        const string VignetteKey    = "retro.vignette";
        const string VolumeKey      = "retro.volume";
        const string ShakeKey       = "retro.screenShake";

        static float _fov         = 60f;
        static float _sensitivity = 0.15f;
        static int   _pixelHeight = 480;
        static float _scanlines   = 0.18f;
        static float _vignette    = 0.35f;
        static float _volume      = 1f;
        static float _shake       = 1f;

        static bool _loaded;

        /// <summary>Vertical FOV. 60+ recommended; low values cause motion sickness.</summary>
        public static float Fov
        {
            get { Load(); return _fov; }
            set { Load(); _fov = Mathf.Clamp(value, 50f, 100f); Raise(); }
        }

        public static float Sensitivity
        {
            get { Load(); return _sensitivity; }
            set { Load(); _sensitivity = Mathf.Clamp(value, 0.02f, 0.6f); Raise(); }
        }

        /// <summary>Vertical resolution the retro pass downsamples to. Lower = chunkier.</summary>
        public static int PixelHeight
        {
            get { Load(); return _pixelHeight; }
            set { Load(); _pixelHeight = Mathf.Clamp(value, 120, 1080); Raise(); }
        }

        public static float ScanlineIntensity
        {
            get { Load(); return _scanlines; }
            set { Load(); _scanlines = Mathf.Clamp01(value); Raise(); }
        }

        public static float VignetteIntensity
        {
            get { Load(); return _vignette; }
            set { Load(); _vignette = Mathf.Clamp01(value); Raise(); }
        }

        public static float Volume
        {
            get { Load(); return _volume; }
            set { Load(); _volume = Mathf.Clamp01(value); Raise(); }
        }

        /// <summary>Impact camera shake multiplier. 0 turns it off for players prone to motion sickness.</summary>
        public static float ScreenShake
        {
            get { Load(); return _shake; }
            set { Load(); _shake = Mathf.Clamp01(value); Raise(); }
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true; // set first: the getters below re-enter Load()

            _fov         = PlayerPrefs.GetFloat(FovKey, _fov);
            _sensitivity = PlayerPrefs.GetFloat(SensitivityKey, _sensitivity);
            _pixelHeight = PlayerPrefs.GetInt(PixelHeightKey, _pixelHeight);
            _scanlines   = PlayerPrefs.GetFloat(ScanlineKey, _scanlines);
            _vignette    = PlayerPrefs.GetFloat(VignetteKey, _vignette);
            _volume      = PlayerPrefs.GetFloat(VolumeKey, _volume);
            _shake       = PlayerPrefs.GetFloat(ShakeKey, _shake);
        }

        static void Raise()
        {
            PlayerPrefs.SetFloat(FovKey, _fov);
            PlayerPrefs.SetFloat(SensitivityKey, _sensitivity);
            PlayerPrefs.SetInt(PixelHeightKey, _pixelHeight);
            PlayerPrefs.SetFloat(ScanlineKey, _scanlines);
            PlayerPrefs.SetFloat(VignetteKey, _vignette);
            PlayerPrefs.SetFloat(VolumeKey, _volume);
            PlayerPrefs.SetFloat(ShakeKey, _shake);
            PlayerPrefs.Save();

            OnChanged?.Invoke();
        }

        /// <summary>Restores the comfort-safe defaults.</summary>
        public static void ResetToDefaults()
        {
            Load();
            _fov = 60f;
            _sensitivity = 0.15f;
            _pixelHeight = 480;
            _scanlines = 0.18f;
            _vignette = 0.35f;
            _volume = 1f;
            _shake = 1f;
            Raise();
        }
    }
}
