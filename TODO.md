# Vampire Survivors Co-op — TODO

> Last updated: 2026-03-21 ~02:30

## Next Up (priority order)

- [ ] CharacterRegistry ScriptableObject (replace hard-coded array in LobbyManager)
- [ ] New characters: Pugnala (needs Phiera Der Tuphello + Eight The Sparrow weapons), Giovanna (needs Gatti Amari weapon)
- [ ] Map variety: second stage tileset + different enemy spawns
- [ ] Hellfire evolution (Fire Wand + Spinach) — wiki: 100 dmg, 3s CD, 2 amount, slow pierce projectiles, fires randomly; NOTE: O'Sole Meeo (Fire Wand + Candelabrador) we shipped is non-canon but keep it
- [ ] La Borra evolution (Holy Water + Attractorb passive) — needs Attractorb passive item first
- [ ] Crimson Shroud (Garlic + Pummarola → Soul Eater already done; check if Crimson Shroud is different weapon)
- [ ] New characters: Pugnala (needs Phiera + Eight The Sparrow twin pistols), Giovanna (needs Gatti Amari)

## Completed

### 2026-03-21 (Session 14 — ~02:30)

- [x] **Attractorb passive** — `PlayerStats.MagnetRadiusMult` ×1.3 per pickup (wiki: ~×1.5/×2/×2.5/×3/×4 over 5 lvls); `XpGemSystem.CollectGemJob` uses `MagnetRadius * StatsLookup[player].MagnetRadiusMult` per player instead of global constant; in upgrade pool: "Attractorb — +30% XP magnet radius"
- [x] **Wings passive** — `PlayerStats.SpeedMult` +0.1 per pickup (wiki: +10% move speed, additive); `PlayerMovementSystem.MoveJob` multiplies `speed.Value * stats.SpeedMult`; in upgrade pool; both fields default 1.0/1.0 via `PlayerAuthoring` bake

### 2026-03-21 (Session 13 — ~02:10)

- [x] **Death Spiral evolution** (Axe + Candelabrador) — `AxeState.IsEvolved=true`; 60 dmg, 4.0s CD; AxeSystem fires 9 piercing scythes in full 360° radial fan (40° apart) at speed 0.8 u/s, MaxRange 20u, `Piercing=true`, no gravity; existing `ProjectileHitSystem` handles pierce-through naturally; `AxeAmount` upgrade blocked when evolved; evolution gate: `AreaMult > 1`

### 2026-03-21 (Session 12 — ~01:45)

- [x] **NO FUTURE evolution** (Runetracer + Armor) — `RunetracerState.IsEvolved=true`; 30 dmg, speed=11, amount=3, bounces=5, 0.35s CD; projectiles `Explodes=true, ExplosionRadius=1.5`; `PendingExplosion` IComponentData added by `ProjectileMovementSystem` on final expire; `ExplosionSystem` (Burst ISystem) deals AoE dmg + knockback in radius to all enemies in range then destroys entity; `RunetracerAmount` upgrade blocked when evolved; evolution gate: `Armor > 0`

### 2026-03-21 (Session 11 — ~01:20)

- [x] **HUD ReviveStocks display** — per-player panel shows "☠×N" label (red, bottom-right) when player has ReviveStocks > 0; hidden otherwise; programmatically created as child of each panel root in `Start()`; `UpdatePanel` polls `ReviveStocks` component via entity array each frame

### 2026-03-21 (Session 10 — ~01:00)

- [x] **Lobby back-navigation** — Escape (keyboard) or B/Circle (gamepad) with no joined players → `SceneManager.LoadScene("2_PressToStartScene")`
- [x] **Ghoul enemy** — dark purple quad (0.6×0.6), HP=120, speed=2.2, dmg=30, XP=10; `GhoulTag` marker; spawns from wave 5 (weight grows to cap 0.12); `GhoulPrefab` field in `SpawnerData`
- [x] **Specter enemy** — pale cyan quad (0.5×0.5), HP=40, speed=4.0, dmg=25, XP=12; `GhostTag` marker; knockback-immune (EnemyMovementSystem zeroes Velocity every frame for GhostTag entities); spawns from wave 7 (weight grows to cap 0.08); `SpecterPrefab` field in `SpawnerData`
- [x] **EnemyMovementSystem split** — `MoveTowardPlayerJob [WithNone(GhostTag)]` applies+decays knockback; `GhostMoveJob [WithAll(GhostTag)]` zeroes knockback each frame (immunity via architecture, no weapon changes needed)

### 2026-03-21 (Session 9 — ~00:30)

- [x] **Krochi Freetto** character — Cross starter, HP=100, Speed=9.1 (+30%), `ReviveStocks{Count=1}` auto-revive; +1 ReviveStock at level 33; added to LobbyManager roster
- [x] **Dommario** character — King Bible starter, HP=100, Speed=4.2 (-40%), DurationMult=1.4, ProjectileSpeedMult=1.4 (+40% Duration+Speed); added to LobbyManager roster
- [x] **ReviveStocks** `IComponentData` — `HealthSystem` checks before marking Downed: if Count>0 auto-revives at 50% HP + 3s iframes and decrements Count; teammate revive still works for characters with Count=0

### 2026-03-21 (Session 8 — ~00:00)

- [x] **Candelabrador passive** — `PlayerStats.AreaMult` ×1.1 per pickup; applied to Garlic range, Whip HitArc range, Holy Water puddle radius, King Bible orbit radius at spawn
- [x] **Spellbinder passive** — `PlayerStats.DurationMult` ×1.1 per pickup; applied to Holy Water puddle lifetime
- [x] **O'Sole Meeo evolution** (Fire Wand + Candelabrador) — `FireWandState.IsEvolved=true`, Amount=8, Damage=20, 0.4s CD; FireAmount upgrade blocked when evolved; gate: `AreaMult > 1`
- [x] **Unholy Vespers evolution** (King Bible + Spellbinder) — `KingBibleState.IsEvolved=true`, Damage=30, Radius=1.75, Count=3; `Spawned=false` triggers re-spawn; cleanup destroys old orbit entities before spawning new ones; gate: `DurationMult > 1`
- [x] **Bone at level 5** — `LevelUpSystem` grants `BoneState` at level 5 for all characters (Mortaccio already has it, guard prevents double-grant); `KingBibleState` still also granted at lv5
- [x] **Runetracer Amount upgrade** — `RunetracerState.Amount` field added (default 1); `RunetracerSystem` fans Amount tracers in 20° spread; `RunetracerAmount` in upgrade pool (cap 5); Duplicator applies to Runetracer; `GameSceneBootstrap` sets Amount=1 for Pasqualina

### 2026-03-20 (Session 7 — ~13:00)

- [x] **Mortaccio** character (Bone starter) — HP=100, Speed=7.0, no stat bonus; added to LobbyManager roster
- [x] **Yatta Cavallo** character (Holy Water starter) — HP=100, Speed=7.0, no stat bonus; added to LobbyManager roster
- [x] **Bone weapon** (`BoneState` + `BoneSystem`) — 30 dmg, 0.5s CD, speed=8, bounces=2, MaxRange=12u; fans Amount bones in 20° spread; `BoneAmount` in upgrade pool; Duplicator applies to Bone

### 2026-03-20 (Session 6 — ~12:30)

- [x] **Duplicator passive** — +1 Amount to ALL currently-owned weapons on pickup; `PlayerStats.DuplicatorStacks` tracks count for Thunder Loop gate; appears in level-up pool (no level cap — each pickup is meaningful)
- [x] **Thunder Loop evolution** (Lightning Ring + Duplicator) — 65 dmg, 6 targets/strike, 0.5s CD; `LightningRingState.IsEvolved` flag; appears in pool when LightningRingState present + !IsEvolved + DuplicatorStacks > 0

### 2026-03-20 (Session 5 — ~12:00)

- [x] **Weapon Amount upgrades for Whip, Axe, Holy Water** in level-up pool
  - Added `Amount` field to `WeaponState` (Whip), `AxeState`, `HolyWaterState`
  - `WhipSystem`: fires `Amount` HitArcs evenly distributed around 360° from facing direction
  - `AxeSystem`: fires `Amount` axes in a 25° fan spread centred on facing direction
  - `HolyWaterSystem`: fires `Amount` flasks in independent random directions
  - `HUDManager`: `WhipAmount`, `AxeAmount`, `HolyWaterAmount` in upgrade pool (cap 5, Whip blocked when evolved)
  - All default to Amount=1; `PlayerAuthoring`, `LevelUpSystem`, `GameSceneBootstrap` updated

### 2026-03-20 (Session 4 — ~11:30)

- [x] **Chest/Treasure System**: enemies drop chests on death (5% base chance, Luck-scaled)
  - `Chest` IComponentData with per-chest `Random Rng` seed
  - `PickupAuthoring.PickupKind.Chest` baker case
  - `SpawnerData.ChestPrefab` entity ref; `SpawnerAuthoring.chestPrefab` field wired
  - `HealthSystem`: instantiates ChestPrefab at death position with unique RNG seed
  - `ChestPickupSystem`: walk-over collect r=0.6u; 4 rewards: 40% gold (100–200), 30% full HP, 20% XP burst (+100), 10% invincibility (8s)
  - Yellow-orange quad prefab (0.4u) at `Assets/Prefabs/Pickups/ChestPrefab.prefab`

### 2026-03-20 (Session 3 — ~10:XX)

- [x] **Hollow Heart** passive: `PlayerStats.MaxHpBonus` +10% of current MaxHp; heals same amount on pickup
- [x] **Bloody Tear** evolution (Whip + Hollow Heart): 20 dmg, heals 1 HP per enemy struck, 0.45s CD; `HitArc.OwnerEntity` + `HealPerHit`; `WeaponState.IsEvolved`
- [x] **Lightning Ring Amount** upgrade in level-up pool (up to 5 strikes/activation)

### 2026-03-20 (Session 2 — ~10:XX)

- [x] **Bracer** passive: `PlayerStats.ProjectileSpeedMult` ×1.1 per pickup; applied in all 6 projectile weapon systems
- [x] **Thousand Edge** evolution (Knife + Bracer): 5 blades, speed 20, 0.15s CD, 15 dmg, 10° tight fan
- [x] **Win condition**: `TriggerVictory()` at 30:00 — green overlay, "YOU SURVIVED!", pauses game; timer flashes yellow < 5min warning

### 2026-03-20 (Session 1 — ~09:XX)

- [x] Created TODO.md, assessed full project state against CLAUDE.md
- [x] **Heaven Sword** evolution (Cross + Clover):
  - Added `IsEvolved` + `Count` fields to `CrossState`
  - Added `Piercing`, `LastPierceHit`, `PierceLockTimer` fields to `Projectile`
  - `ProjectileMovementSystem`: ticks PierceLockTimer, clears LastPierceHit when expired
  - `ProjectileHitSystem`: piercing projectiles pass through enemies (0.3s per-enemy re-hit lockout)
  - `CrossSystem`: when evolved, fires `Count` (2) swords at ±15° from facing, no return, speed=20, MaxRange=20u
  - `HUDManager`: `HeavenSwordEvolution` in upgrade pool when `CrossState` present + `!IsEvolved` + `Luck > 0`; apply sets 200 dmg / 2.5s CD / speed 20

---

## Feature Notes

### Heaven Sword (Cross + Clover)
- Wiki stats: 200 dmg, 2.5s CD, speed 20, pierces all enemies, fires 2 simultaneously at ±15° from facing
- Condition: has CrossState + !IsEvolved + Luck > 0 (at least one Clover taken)

### Bracer / Thousand Edge
- Bracer: new `PlayerStats.ProjectileSpeedMult` field (+10% per level)
- Thousand Edge: Knife + Bracer — fires 5 knives, doubled speed, high damage

### Hollow Heart / Bloody Tear
- Hollow Heart: new `PlayerStats.MaxHpMult` field (+10% max HP per level, updates MaxHp in HealthSystem)
- Bloody Tear: Whip + Hollow Heart — whip heals 1 HP per enemy struck

### Win Condition (30-min run)
- HUDManager counts up to 30:00; at 30:00 trigger `TriggerVictory()` instead of game over
- Show "YOU SURVIVED!" screen with same stats as game over
- Optional: spawn unkillable Death boss at 30:00 (deferred)

### Chest System (COMPLETE)
- `HealthSystem`: 5% base drop chance on enemy death, scaled by team Luck; seeds RNG from position + kill count
- `ChestPickupSystem`: walk-over collect r=0.6u; per-chest `Random` for independent variance
  - 40% gold bonus (100–200 → SharedGold), 30% full HP restore, 20% XP burst (+100), 10% invincibility 8s
- Prefab: yellow-orange Quad 0.4u at `Assets/Prefabs/Pickups/ChestPrefab.prefab`
