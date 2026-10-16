# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-10-16

### Added
- `SaveSystem.AutosaveSlot`, written to `autosave.json` so automatic saves never overwrite manual slots.
- `SaveSystem.LoadHandler` and `SaveSystem.RequestLoad`, so a game can replace the in-place load
  (e.g. reload the scene before restoring). The pause menu now loads through `RequestLoad`.
- `SaveSystem.Saved` event, raised after a save is written.
- `SaveSlotPanel` optional autosave button, shown on the Load panel only.
- `OptionsPanel` FOV range is now configurable in the inspector (default 50-100); stored values
  outside it are pulled into range.
- Quest System adapter: `QuestSaveParticipant` persists the active chapter, quest and every
  objective's progress, and resumes auto-advance for a quest saved the moment it finished.

## [1.0.0] - 2026-09-20

### Added
- Esc-driven pause menu with Continue / Save / Load / Options / Quit.
- `MenuRegistry` plus the `IPauseTarget` / `ISettingsTarget` / `ISaveParticipant`
  contracts, keeping the core assembly free of any game-specific references.
- `SaveSystem` with three JSON save slots, format versioning and slot metadata preview.
- `GameSettings`, a PlayerPrefs-backed store for FOV, sensitivity, pixel height,
  scanline intensity, vignette intensity and volume.
- `OptionsPanel` and `SaveSlotPanel` UI components.
- `PlayerTransformSaveParticipant` for persisting player position and rotation.
- Optional adapter assemblies, each gated behind a `versionDefine` so they compile only
  when their package is installed:
  - First Person Controller — pause handling, FOV and sensitivity.
  - Retro Inventory — inventory contents in the save file.
  - Retro PS1 Post-Processing — shader settings from the options sliders, with
    snapshot/restore so Editor play sessions cannot overwrite authored defaults.
- Standalone UPM package layout with assembly definitions.
