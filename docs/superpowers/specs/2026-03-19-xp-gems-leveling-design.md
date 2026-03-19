# XP Gems & Leveling — Design Spec

**Goal:** Close the kill → reward loop. Enemies drop XP gems on death; players walk near them to collect; XP fills a bar and levels the player up.

**Architecture:** Pure ECS/DOTS. New `XpGem` component. Three new systems. `HealthSystem` modified to spawn gems. `PlayerAuthoring` fix: XpToNextLevel starts at 5.

---

## Components (additions to EnemyComponents.cs)

```csharp
XpGem : IComponentData { float Value; }
```

`PlayerStats.Xp` and `PlayerStats.XpToNextLevel` already exist in `PlayerComponents.cs`. No structural changes needed to PlayerStats.

---

## XP Values (from wiki, already set in EnemyStats)

| Enemy    | XpValue |
|----------|---------|
| Bat      | 1       |
| Zombie   | 5       |
| Skeleton | 10      |

---

## Level-Up XP Requirements (from wiki)

| Level | XP needed to reach next level |
|-------|-------------------------------|
| 1→2   | 5                             |
| 2→3   | 15                            |
| 3→4   | 25                            |
| 4→5   | 35                            |
| N→N+1 | 5 + (N-1) × 10               |

`PlayerAuthoring` currently bakes `XpToNextLevel = 100f`. Fix to `5f`.

---

## Gem Behavior (from wiki)

- **Drop:** spawned at enemy death position by HealthSystem
- **Attraction radius (magnet):** 30 units — when player is within this distance, gem homes toward player
- **Gem move speed:** 8 units/sec (not in wiki; tuned for feel)
- **Collection radius:** 0.5 units — gem is absorbed and XP added to player
- **Visual:** small yellow quad (scale 0.3), same quad approach as enemies — replace with sprite later

---

## Systems

### `HealthSystem` changes (existing file)
When destroying an enemy entity, if it has `EnemyStats`, spawn an `XpGem` entity via ECB at the enemy's `LocalTransform.Position` with `Value = stats.XpValue`. Also spawn a `LocalTransform` so XpGemSystem can query position.

### `XpGemSystem` (new, Burst)
For each `XpGem` entity with `LocalTransform`:
- Build NativeArray of player positions and entities (same pattern as ContactDamageSystem)
- Find nearest player within `MagnetRadius = 30f`
- If found: move gem toward player at `GemSpeed = 8f`
- If within `CollectRadius = 0.5f`: add `gem.Value` to nearest player's `PlayerStats.Xp` via ComponentLookup, destroy gem via ECB
- Runs single-threaded (`.Run()`) — multiple gems can hit same player

### `LevelUpSystem` (new, not Burst — calls Debug.Log)
For each player entity with `PlayerStats + PlayerIndex + Invincible`:
- While `Stats.Xp >= Stats.XpToNextLevel`:
  - Subtract `XpToNextLevel` from `Xp`
  - Increment `Level`
  - Set `XpToNextLevel = 5f + (Level - 1) * 10f`
  - Set `Invincible.Timer = max(Timer, 2f)` (brief iframes)
  - `Debug.Log($"[LevelUpSystem] Player {idx.Value} reached level {stats.Level}!")`
- Level-up UI (weapon choice) is future work

---

## PlayerAuthoring fix

Change `XpToNextLevel = 100f` → `XpToNextLevel = 5f` in Baker.

---

## XP Gem Prefab

A small yellow Quad, scale (0.3, 0.3, 1), with `XpGemAuthoring` component (just marks it with `XpGem { Value = 1 }`). The actual value is overridden when spawned by HealthSystem via ECB (set directly on the spawned entity).

Since HealthSystem creates gems programmatically (not via prefab instantiation), no prefab is strictly needed for function — but having one is useful for future scene placement and testing. For now, gems are spawned as raw entities via ECB with `AddComponent<XpGem>` and `AddComponent<LocalTransform>`.

---

## File Map

| Action | File |
|--------|------|
| Modify | `Assets/Scripts/Components/EnemyComponents.cs` — add `XpGem` struct |
| Modify | `Assets/Scripts/Authoring/PlayerAuthoring.cs` — XpToNextLevel = 5 |
| Modify | `Assets/Scripts/Systems/HealthSystem.cs` — spawn gem on enemy death |
| Create | `Assets/Scripts/Systems/XpGemSystem.cs` |
| Create | `Assets/Scripts/Systems/LevelUpSystem.cs` |
