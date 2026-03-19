# Menu Scene Design

**Date:** 2026-03-19
**Project:** Vampire Survivors Co-op Clone
**Platform:** Apple TV (tvOS, primary) + standalone

---

## Overview

A three-screen menu flow that precedes gameplay. Designed for local co-op with up to 4 players, Apple TV Remote as P1's primary input, and Bluetooth controllers (Xbox, PS, MFi) for additional players. Character and customization choices are remembered per device across sessions.

---

## Screen Flow

```
SplashScene → PressToStartScene → LobbyScene → GameScene
```

### 1. SplashScene
- Displays the game logo (and optionally a studio logo)
- Fades in, holds briefly, fades out
- Auto-advances to PressToStartScene after ~2.5s
- Any button press skips the wait

### 2. PressToStartScene
- Full-screen prompt: "Press any button to start"
- Input is ignored for the first 0.3s after scene load to prevent spurious tvOS Remote events (OS-level wakes/navigations can fire input events into the app)
- The first device to press any button after the cooldown is assigned as P1
- Advances immediately to LobbyScene, carrying the P1 device assignment

### 3. LobbyScene
- Combined title screen and co-op lobby
- Displays game logo/title at top
- Four player slots across the center
- Settings button accessible from this screen
- PLAY button (bottom center) — active when ≥1 player slot is filled; any joined player can confirm

### 4. GameScene (existing SampleScene)
- Receives player slot assignments via `GameSession`
- `GameSceneBootstrap` reads session data and configures player entities (see Scene Handoff)

---

## Lobby Slot Behaviour

Each slot has two states: **Empty** and **Joined**. All joined players are considered ready when PLAY is pressed.

| State | Visual | Available Input |
|-------|--------|-----------------|
| Empty | Controller icon + "Press any button to join" | Any button on unclaimed device → joins |
| Joined | Character portrait, name, customization | Up/Down = cycle character, Left/Right = cycle customization, UI.Cancel (`*/{Cancel}`) = leave slot |

- Joining claims the next available slot in order (P1 → P2 → P3 → P4)
- A device can only occupy one slot at a time
- Leaving a slot releases the device; the slot becomes empty (does not shift other players)

### Device Disconnect Mid-Lobby
- If a device disconnects while its slot is Joined, the slot reverts to Empty
- The PLAY button re-evaluates the ≥1 filled slot condition
- No reconnect prompt shown for MVP

---

## Character & Customization Selection

- **Up/Down** (stick or D-pad, read directly from `Gamepad.leftStick` / `Gamepad.dpad`) cycles through the available character roster
- **Left/Right** cycles through that character's available customizations (skins, hats)
- Lobby navigation reads `InputDevice` state directly — no `PlayerInput` component, consistent with the manual polling approach used in `PlayerInputSystem`
- **Input thresholds:** axis dead zone = 0.5; after an initial press, repeat interval = 0.25s to prevent rapid unintentional cycling
- The selected character + customization index are saved to `PlayerPrefs` immediately on change
- On join, the slot loads the device's last saved selection; defaults to first character/customization if no save exists

### Focus Model Boundary (tvOS)
- Per-device polling (Up/Down/Left/Right) exclusively controls each player's own slot — character and customization cycling is never driven by tvOS focus navigation
- tvOS focus navigation applies **only** to the PLAY button and the Settings button
- Slots themselves are not focusable UI elements; they respond only to their owning device's input

---

## Per-Device Persistence

- **Key:** `InputDevice.description.serial` when non-empty; otherwise `description.product + description.manufacturer` combined. Two identical controllers with no serial (common on tvOS) will share a key — last-write-wins is accepted for MVP
- **Value:** JSON blob — `{ "characterId": "antonio", "customizationIndex": 2 }`
- **Storage:** `PlayerPrefs`
- **Valid character IDs** (lowercase ASCII, matching a future `CharacterRegistry` ScriptableObject): `antonio`, `imelda`, `pasqualina`, `gennaro`
- **Fallback:** If description yields an empty string, fall back to slot index as key

---

## Persistent Session Object

`GameSession` — a `MonoBehaviour` singleton with `DontDestroyOnLoad`.

Carries from Lobby → Game:
- Per-slot: `InputDevice` reference, character ID, customization index
- Player count

**Lifecycle:** `LobbyManager.Awake` calls `GameSession.Instance?.DestroySelf()` on scene load, ensuring a clean slate on every lobby visit. This covers the "return from game to menu" case — `GameSession` survives the scene transition but is destroyed as soon as `LobbyScene` loads.

---

## Scene Handoff (Lobby → Game)

`GameSceneBootstrap` is a MonoBehaviour in `GameScene` that runs on `Start`:

1. If `GameSession` is null (e.g. scene loaded directly during development), skip handoff and leave the baked-in `PlayerAuthoring` entities as-is — this preserves the existing dev workflow
2. Otherwise, reads `GameSession.Slots` (list of filled slots, each with `InputDevice` reference, character ID, customization index)
3. For each filled slot, locates the corresponding player entity (matched by slot/`PlayerIndex`)
4. Adds an `AssignedDeviceId` component (`int deviceId = slot.Device.deviceId`) to the entity

`PlayerInputSystem` is updated to look up by `AssignedDeviceId` instead of `Gamepad.all[i]`. The `PlayerIndex` doc comment on `PlayerComponents.cs` line 9 (`/// Maps this entity to Gamepad.all[Value]`) must also be updated to reflect that `PlayerIndex` is now a slot index only, not a `Gamepad.all` index:
- For each entity with `AssignedDeviceId`, find the matching `InputDevice` via `InputSystem.GetDeviceById(deviceId)`
- Cast to `Gamepad` and read `leftStick` as before
- If the device is no longer connected, write `float2.zero` (existing fallback behaviour)

This is a small, contained change to `PlayerInputSystem` — the polling logic is unchanged, only the device lookup differs. `Gamepad.all` ordering is no longer relied upon.

---

## Settings Screen

Accessible from the Lobby via a Settings button. Navigated with focus-based input (tvOS requirement).

**Focus order on open:** Master Volume → Music Volume → SFX Volume → Back

| Setting | Type |
|---------|------|
| Master Volume | Slider 0–100 |
| Music Volume | Slider 0–100 |
| SFX Volume | Slider 0–100 |

Settings persisted via `PlayerPrefs`. Back button returns to Lobby.

---

## Apple TV Considerations

- Primary platform is tvOS (Apple TV)
- tvOS uses **focus-based navigation** — all UI elements must be focusable and navigable via the Apple TV Remote's touch surface/D-pad
- Apple TV Remote support handled by an existing third-party asset (to be integrated later); until then, Bluetooth controllers only
- Spurious input events during scene load mitigated by 0.3s cooldown in PressToStartScene

---

## Architecture

### Scenes
| Scene | Path |
|-------|------|
| Splash | `Assets/Scenes/SplashScene.unity` |
| Press to Start | `Assets/Scenes/PressToStartScene.unity` |
| Lobby | `Assets/Scenes/LobbyScene.unity` |
| Game | `Assets/Scenes/SampleScene.unity` |

### Key Scripts
| Script | Type | Purpose |
|--------|------|---------|
| `GameSession.cs` | MonoBehaviour (DontDestroyOnLoad singleton) | Carries slot data Lobby → Game |
| `LobbyManager.cs` | MonoBehaviour | Device assignment, slot state, input polling |
| `PlayerSlotUI.cs` | MonoBehaviour | Visual state for a single slot |
| `DeviceSaveData.cs` | Plain C# | PlayerPrefs key generation + JSON serialization |
| `GameSceneBootstrap.cs` | MonoBehaviour | Reads GameSession, configures player entities on game start |
| `SplashController.cs` | MonoBehaviour | Logo timing and skip |
| `PressToStartController.cs` | MonoBehaviour | Input cooldown, P1 device claim, scene advance |
| `AssignedDeviceId` (added to `PlayerComponents.cs`) | IComponentData | Stores `InputDevice.deviceId` on a player entity so `PlayerInputSystem` can look up the correct device regardless of `Gamepad.all` order. Lives in `PlayerComponents.cs` alongside the other gameplay components — it is set by the menu layer but consumed by gameplay, so it belongs in the shared component file |

### Input
- Unity Input System — `InputSystem.onDeviceChange` to detect connect/disconnect
- Device assignment tracked in `LobbyManager` as `Dictionary<InputDevice, int>` (device → slot index)
- No `PlayerInput` component — manual device polling throughout
- Leave slot action: `UI.Cancel` (`*/{Cancel}` — B / Circle / Escape)

---

## Out of Scope (this iteration)
- Apple TV Remote integration (asset exists, wire in later)
- Controller disconnect during gameplay — no handling for MVP; affected player input freezes, game continues
- Online multiplayer lobby
- Character unlock gating
- Player profiles / accounts
- Animated 3D background scene — static background image for now
