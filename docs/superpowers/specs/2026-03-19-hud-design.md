# HUD System — Design Spec

**Goal:** A screen-space HUD that shows per-player HP bars, XP bars, and level numbers in the four screen corners, plus a top-center elapsed game timer. Makes the XP/leveling system visible and HP status legible.

**Architecture:** Single `HUDManager` MonoBehaviour on a Canvas in `4_SampleScene`. Polls ECS world each frame for player data. No ECS system needed — this is UI mutation (managed calls), per project convention ("MonoBehaviours are only used for scene bootstrapping and UI").

---

## Components Queried

- `PlayerTag` — filter predicate
- `PlayerIndex { int Value }` — maps entity → panel slot (0–3)
- `PlayerStats { int Level; float Xp; float XpToNextLevel; }` — XP bar + level label
- `Health { int Current; int Max; }` — HP bar (authoritative combat HP, not PlayerStats.Hp)

---

## Layout

Four 220×80 px panels anchored to screen corners, 20 px inset:

| Slot | Corner | Anchor |
|------|--------|--------|
| 0 | Bottom-left | (0,0) |
| 1 | Bottom-right | (1,0) |
| 2 | Top-left | (0,1) |
| 3 | Top-right | (1,1) |

Each panel contains:
- `PlayerLabel` — "P1"/"P2"/etc., top-left of panel
- `LevelText` — "Lv N", top-right of panel
- `HPBarBG > HPFill` — horizontal fill bar, green/yellow/red by ratio
- `XPBarBG > XPFill` — horizontal fill bar, purple

Timer: top-center, 160×40 px, "MM:SS" format.

## HP Color Coding

| Ratio | Color |
|-------|-------|
| > 50% | Green (0.20, 0.80, 0.20) |
| 25–50% | Yellow (0.90, 0.80, 0.10) |
| < 25% | Red (0.85, 0.15, 0.15) |

---

## Data Access

`HUDManager.Start()` creates an `EntityQuery` using `World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery()`. `Update()` calls `ToComponentDataArray<T>(Allocator.Temp)` for each component type separately (Entities 1.3.14 doesn't have a multi-type tuple API). Arrays are disposed immediately after use. Pattern matches `CameraFollow.cs`.

---

## Active Player Detection

Panels are `SetActive(false)` by default. Each frame, `HUDManager` tracks which `PlayerIndex.Value` slots were seen. Slots not seen are hidden. Handles 0–4 players cleanly. In dev mode (direct scene load, no lobby), all 4 baked player entities are present → all 4 panels show.

---

## Tech Notes

- Uses `TMPro.TMP_Text` (already in project via `Unity.TextMeshPro`)
- Canvas: Screen Space - Overlay, Scale With Screen Size 1920×1080, Match 0.5
- `_elapsedTime` accumulates via `Time.deltaTime` — resets on scene reload (correct for per-run timer)
- `PlayerIndex.Value` is `int` — safe for array indexing 0–3
- `Health` is shared struct (EnemyComponents.cs) — `PlayerTag` filter ensures only player entities

---

## File Map

| Action | File |
|--------|------|
| Create | `Assets/Scripts/MonoBehaviours/HUDManager.cs` |
| Modify | `Assets/Scenes/4_SampleScene.unity` — add Canvas + HUD hierarchy |
