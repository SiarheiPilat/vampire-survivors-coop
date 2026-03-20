# Vampire Survivors Co-op — TODO

> Last updated: 2026-03-20 ~12:30

## Next Up (priority order)

- [ ] More characters: Mortaccio (Bone), Yatta Cavallo (Santa Water)
- [ ] CharacterRegistry ScriptableObject (replace hard-coded array in LobbyManager)
- [ ] Back-navigation from Lobby (B/Circle with no joined players → PressToStart)
- [ ] Weapon evolutions: Unholy Vespers (King Bible + Spellbinder passive), O'Sole Meeo (Fire Wand + Candelabrador), Bone evolution

## Completed

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
