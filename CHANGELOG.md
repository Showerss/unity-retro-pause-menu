# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
