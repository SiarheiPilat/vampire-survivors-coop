# Player System — Design Spec
**Date:** 2026-03-18
**Status:** Approved

---

## Overview

Implements the foundational player system: up to 4 players on a single machine, each driven by a dedicated gamepad. Covers ECS components, authoring/baking, input routing, movement, colored quad visuals, and a camera that follows the centroid of all players with dynamic zoom.

---

## Components

All in `Assets/Scripts/Components/PlayerComponents.cs`.

| Struct | Interface | Fields |
|--------|-----------|--------|
| `PlayerTag` | `IComponentData` | *(zero-size marker)* |
| `PlayerIndex` | `IComponentData` | `byte Value` — maps to `Gamepad.all[Value]` |
| `MoveInput` | `IComponentData` | `float2 Value` — written each frame by input system |
| `MoveSpeed` | `IComponentData` | `float Value` — base units/sec |
| `PlayerStats` | `IComponentData` | `int Hp, MaxHp, Level; float Xp, XpToNextLevel` |

---

## Authoring

`Assets/Scripts/Authoring/PlayerAuthoring.cs`
MonoBehaviour + Baker. Fields: `playerIndex (byte)`, `moveSpeed (float, default 7f)`, `maxHp (int, default 100)`.
Baker adds all five components to the entity and sets initial values. `XpToNextLevel` defaults to 100f, level to 1.

---

## Systems

### PlayerInputSystem
`Assets/Scripts/Systems/PlayerInputSystem.cs`
- `ISystem`, NOT Burst-compiled (reads managed `Gamepad.all`)
- Both systems rely on the default `SimulationSystemGroup` (no `[UpdateInGroup]` needed); `PlayerInputSystem` is attributed `[UpdateBefore(typeof(PlayerMovementSystem))]` — this ordering attribute is only enforced within the same group, and since both land in `SimulationSystemGroup` by default, the guarantee holds
- `OnUpdate`: foreach player entity with `(PlayerIndex, ref MoveInput)`, check `Gamepad.all.Count > index` before reading; if the gamepad isn't connected, write `float2.zero` into `MoveInput.Value` — no exception thrown
- If the check passes, read `Gamepad.all[index].leftStick.ReadValue()` and write into `MoveInput.Value`

### PlayerMovementSystem
`Assets/Scripts/Systems/PlayerMovementSystem.cs`
- `ISystem`, `[BurstCompile]`
- `IJobEntity` reads `MoveInput`, `MoveSpeed`, `ref LocalTransform`
- Moves in XY plane: `position.xy += input.Value * speed * deltaTime`
- Z stays 0

---

## Visuals

No sprites yet. Each player entity gets a colored quad material so they're visually distinct:

| Player | Color |
|--------|-------|
| 0 | Red |
| 1 | Blue |
| 2 | Green |
| 3 | Yellow |

`PlayerAuthoring` exposes a serialized `Material[] playerMaterials` field (4 slots, one per player color — assign in Inspector). Baker uses `playerMaterials[playerIndex]` directly; no `Shader.Find` in the Baker (unsafe during baking/asset import). Uses `RenderMeshUtility.AddComponents` with `RenderMeshDescription` + `RenderMeshArray` — same 1.4.x API pattern confirmed in ECSBootstrap (`RenderMeshDescription(ShadowCastingMode.Off, receiveShadows: false)`, `RenderMeshArray(materials, meshes)`, `MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)`).

---

## Camera

`Assets/Scripts/MonoBehaviours/CameraFollow.cs`
Plain MonoBehaviour on the Main Camera (camera stays managed — no need to DOTS it).

- `Start`: create and cache an `EntityQuery` via `World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadOnly<LocalTransform>())`
- `OnDisable`: dispose the cached query to avoid memory leaks
- Both `Start` and `LateUpdate` null-guard `World.DefaultGameObjectInjectionWorld` — exits safely between play-mode sessions and during shutdown
- `LateUpdate`: call `query.ToComponentDataArray<LocalTransform>(Allocator.Temp)` to collect positions; iterate it; call `.Dispose()` on the `NativeArray` before `LateUpdate` returns (failure to dispose causes native memory leak warnings in development builds)
- **Position**: lerps camera XY toward centroid of all player positions (speed: 5f)
- **Zoom**: orthographic size lerps toward `baseSize + spread * zoomFactor`
  - `spread` = max distance from centroid to any player
  - `baseSize = 5f`, `zoomFactor = 0.4f`, `maxSize = 12f`
- Z stays fixed at -10

---

## Scene Setup

- Remove (or disable) `ECSBootstrap` from `SampleScene`
- Add 4 GameObjects named `Player_0` through `Player_3`, each with `PlayerAuthoring` (indices 0–3)
- Main Camera gets `CameraFollow` component

---

## File Map

```
Assets/Scripts/
  Components/
    PlayerComponents.cs       ← all 5 component structs
  Authoring/
    PlayerAuthoring.cs        ← MonoBehaviour + Baker
  Systems/
    PlayerInputSystem.cs      ← reads gamepads → MoveInput
    PlayerMovementSystem.cs   ← MoveInput → LocalTransform
  MonoBehaviours/
    CameraFollow.cs           ← centroid follow + zoom
```

---

## Out of Scope (next milestones)

- Keyboard fallback for player 0
- Player animations / real sprites
- Collision
- Enemy system
- XP / leveling logic (components exist but no system yet)
