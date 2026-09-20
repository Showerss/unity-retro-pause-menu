using System.Collections.Generic;

namespace Retro.Menu
{
    /// <summary>
    /// Something that must stop while the game is paused - a player controller,
    /// an AI director, a HUD that reads raw input.
    /// </summary>
    public interface IPauseTarget
    {
        void OnPauseChanged(bool paused);
    }

    /// <summary>
    /// Something that reacts to <see cref="GameSettings"/> changing.
    /// </summary>
    public interface ISettingsTarget
    {
        void ApplySettings();
    }

    /// <summary>
    /// Something that contributes a blob to the save file. Keep <see cref="SaveKey"/>
    /// stable across versions - it is the only link between a save on disk and the
    /// system that wrote it. Unknown keys are ignored on load, so adding a new
    /// participant never invalidates existing saves.
    /// </summary>
    public interface ISaveParticipant
    {
        string SaveKey { get; }
        string Capture();
        void Restore(string json);
    }

    /// <summary>
    /// Registry the menu talks to instead of referencing concrete game systems.
    ///
    /// This is what lets this package import into a project that has no first-person
    /// controller, no inventory and no retro shader: nothing here names those types.
    /// Integrations register themselves from OnEnable and drop off in OnDisable.
    /// </summary>
    public static class MenuRegistry
    {
        static readonly List<IPauseTarget> _pauseTargets = new List<IPauseTarget>();
        static readonly List<ISettingsTarget> _settingsTargets = new List<ISettingsTarget>();
        static readonly List<ISaveParticipant> _saveParticipants = new List<ISaveParticipant>();

        public static IReadOnlyList<IPauseTarget> PauseTargets => _pauseTargets;
        public static IReadOnlyList<ISettingsTarget> SettingsTargets => _settingsTargets;
        public static IReadOnlyList<ISaveParticipant> SaveParticipants => _saveParticipants;

        /// <summary>Registers an object under every contract it implements.</summary>
        public static void Register(object o)
        {
            if (o is IPauseTarget p && !_pauseTargets.Contains(p)) _pauseTargets.Add(p);
            if (o is ISettingsTarget s && !_settingsTargets.Contains(s)) _settingsTargets.Add(s);
            if (o is ISaveParticipant v && !_saveParticipants.Contains(v)) _saveParticipants.Add(v);
        }

        public static void Unregister(object o)
        {
            if (o is IPauseTarget p) _pauseTargets.Remove(p);
            if (o is ISettingsTarget s) _settingsTargets.Remove(s);
            if (o is ISaveParticipant v) _saveParticipants.Remove(v);
        }

        /// <summary>
        /// Editor play-mode reloads can leave dead entries behind; the menu clears
        /// on Awake so a stale list never silently swallows a pause.
        /// </summary>
        public static void Clear()
        {
            _pauseTargets.Clear();
            _settingsTargets.Clear();
            _saveParticipants.Clear();
        }
    }
}
