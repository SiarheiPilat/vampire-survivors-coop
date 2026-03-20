# Vampire Survivors Co-op — TODO

> Last updated: 2026-03-20

## Next Up (priority order)

- [ ] Hollow Heart passive (+10% max HP) + Bloody Tear evolution (Whip + Hollow Heart)
- [ ] Chest/treasure system: enemies drop chests on death, reward on contact (weapon/passive/gold)
- [ ] Chest/treasure system: enemies drop chests on death, reward on contact (weapon/passive/gold)
- [ ] Weapon amount upgrades for Whip, Axe, Cross, HolyWater, LightningRing in level-up pool
- [ ] More characters: Mortaccio (Bone), Yatta Cavallo (Santa Water)
- [ ] Thunder Loop evolution (Lightning Ring + Duplicator passive)
- [ ] Duplicator passive (+1 Amount to all weapons)
- [ ] CharacterRegistry ScriptableObject (replace hard-coded array in LobbyManager)
- [ ] Back-navigation from Lobby (B/Circle with no joined players → PressToStart)

## Completed

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

### Chest System
- HealthSystem: 5% chance on enemy death to drop a `Chest` entity (yellow-green quad 0.4u)
- `ChestPickupSystem`: walk-over collect (r=0.5u), award random item from weighted table
  - 40% gold (25–100), 30% passive item, 20% weapon upgrade, 10% full HP restore
