# Retro Pause Menu

An Esc-driven pause menu for Unity: Continue / Save / Load / Options / Quit, with
slot-based JSON saves and a PlayerPrefs settings store.

The core assembly names no game systems at all. It talks to your game through three
small interfaces, so it imports cleanly into a project with no player controller, no
inventory and no custom shader.

## Architecture Overview

```
Runtime/  (Retro.PauseMenu - zero external dependencies)
├── PauseMenuController   freezes time, shows panels, broadcasts pause state
├── MenuRegistry          the registry everything else plugs into
├── GameSettings          static PlayerPrefs-backed settings + OnChanged
├── SaveSystem            3 JSON save slots on disk
├── OptionsPanel          sliders bound to GameSettings
└── SaveSlotPanel         save/load slot UI

Adapters/  (each a separate assembly, compiled only if its package is installed)
├── FirstPersonController/  disables the controller + PlayerInput on pause, applies FOV/sensitivity
├── Inventory/              persists inventory contents into the save file
└── RetroPS1/               drives pixel height / scanlines / vignette from the options sliders
```

### The three contracts

| Interface | Meaning | Implement it when |
|---|---|---|
| `IPauseTarget` | `OnPauseChanged(bool)` | Something must stop while paused |
| `ISettingsTarget` | `ApplySettings()` | Something reacts to a settings change |
| `ISaveParticipant` | `SaveKey` / `Capture()` / `Restore(json)` | Something belongs in the save file |

Register from `OnEnable`, unregister from `OnDisable`:

```csharp
void OnEnable()  => MenuRegistry.Register(this);
void OnDisable() => MenuRegistry.Unregister(this);
```

`SaveKey` is the only link between a blob on disk and the system that wrote it — keep it
stable across versions. Unknown keys are ignored on load, so adding a new participant
never invalidates existing saves.

### Why pausing is not just `Time.timeScale = 0`

A look script that rotates by `input * sensitivity` with no `Time.deltaTime` factor keeps
working at timeScale 0, because `Update` still runs. Freezing time is necessary but not
sufficient — that is why `IPauseTarget` exists and why the FPC adapter switches the
controller component off rather than trusting the clock.

---

## Installation

### Option 1: Via Git URL (Package Manager)
1. In Unity, open **Window > Package Manager**.
2. Click the **`+`** icon in the upper-left corner and choose **Add package from git URL...**
3. Enter:
   ```text
   https://github.com/Showerss/unity-retro-pause-menu.git
   ```

### Option 2: Via `manifest.json`
Add this line to your project's `Packages/manifest.json` under `"dependencies"`:
```json
"com.retro.pausemenu": "https://github.com/Showerss/unity-retro-pause-menu.git"
```
Or for local development:
```json
"com.retro.pausemenu": "file:../path/to/Packages/com.retro.pausemenu"
```

## Quick Start

1. Build a Canvas with a root object and four child panels: main, options, save, load.
2. Add **`PauseMenuController`** to the Canvas and assign those five objects.
3. Add **`OptionsPanel`** to your options panel and bind its sliders.
4. Add **`SaveSlotPanel`** to the save and load panels.
5. Press **Esc** in play mode.

Nothing else is required — an unadapted game will pause (time freezes, menu appears) and
save/load will work for whatever participants you have registered.

## Optional adapters

Each adapter lives in its own assembly behind a `versionDefine`, so it compiles only when
the matching package is present. Install the package and the adapter simply appears; skip
it and nothing breaks.

| Adapter | Needs | Add it to | Does |
|---|---|---|---|
| `FirstPersonControllerAdapter` | `com.firstperson.controller` | Player | Disables controller + PlayerInput on pause; applies FOV and mouse sensitivity |
| `InventorySaveParticipant` | `com.retro.inventory` | The `InventorySystem` object | Writes inventory contents into the save file |
| `RetroPS1SettingsTarget` | `com.retro.ps1postprocess` | Any scene object | Drives pixel height, scanlines and vignette from the options sliders |
| `ScreenShakeSettingsTarget` | `com.retro.combat` | Next to `SettingsApplier` | Drives `ScreenShake.Strength` from the Screen Shake slider |
| `LightSwitchSaveState` | `com.firstperson.controller` | A `LightSwitch` with a `SaveableObject` | Saves whether the lamp is lit |
| `QuestSaveParticipant` | `com.retro.questsystem` | The `QuestManager` object | Saves chapter, quest and objective progress |
| `QuestAutosave` | `com.retro.questsystem` | The `QuestManager` object | Autosaves on objective / quest / chapter completion |
| `StoryFlagsSaveParticipant` | `com.retro.questsystem` | A persistent object | Saves every story flag |
| `SequenceSaveState` | `com.retro.questsystem` | A `GameSequence` with a `SaveableObject` | Saves whether the sequence finished |

With the quest system installed, sequences also never start while a save loads, autosaves wait for playing sequences, and flag triggers re-check after a load. No setup needed.

> **Note on `RetroPS1SettingsTarget`:** the render feature's settings live on a
> `UniversalRendererData` asset. Writing them in the Editor dirties that asset on disk and
> the change survives exiting play mode, so a play session spent dragging sliders would
> silently overwrite your authored defaults. The adapter snapshots on enable and restores
> on disable to prevent exactly that.

`PlayerTransformSaveParticipant` ships in the core assembly and needs no package — drop it
on the player to persist position and rotation.

## Settings

`GameSettings` is a static PlayerPrefs-backed store with an `OnChanged` event:

```csharp
GameSettings.Fov = 75f;
GameSettings.Sensitivity = 1.2f;
GameSettings.PixelHeight = 240;
GameSettings.ScanlineIntensity = 0.3f;
GameSettings.VignetteIntensity = 0.4f;
GameSettings.Volume = 0.8f;

GameSettings.ResetToDefaults();
GameSettings.OnChanged += () => Debug.Log("settings changed");
```

## Saving

```csharp
SaveSystem.SlotCount;        // 3
SaveSystem.SlotExists(0);
SaveSystem.Peek(0);          // metadata without loading
SaveSystem.Save(0);
SaveSystem.Load(0);
SaveSystem.Delete(0);
```

`Save` walks every registered `ISaveParticipant`, collects its `Capture()` blob under its
`SaveKey`, and writes one JSON file per slot. `SaveSystem.AutosaveSlot` (-1) writes
`autosave.json`, separate from the manual slots.

### Scene-aware saving (optional)

For a game with a persistent Boot scene that loads gameplay scenes additively:

1. Put `GameSaveService` on a persistent object in Boot. The menu's Load now reloads the saved
   scene and restores it before anything in it runs `Start`.
2. Put `WorldStateSaveParticipant` in each gameplay scene, and a `SaveableObject` on every object
   whose state should survive (pickups, lamps, revealed or moved props). Add a component that
   implements `ISaveableState` next to it to save more.
3. Call `GameSaveService.RequestAutosave()` whenever progress is made (or use `QuestAutosave`),
   and add an `AutosaveIndicator` to the menu canvas.

## License

[MIT License](LICENSE.md) © 2026 Phillip Banky
