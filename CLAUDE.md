# Vampire Survivors Co-op Clone — CLAUDE.md

## Project Overview

A co-op clone of Vampire Survivors built in Unity 6 using DOTS/ECS architecture.
Players survive increasingly difficult enemy waves by collecting weapons, leveling up, and combining upgrades.
The key addition over the original: **local and/or online co-op** with 2–4 players.

---

## Architecture

**Engine**: Unity 6000.3.8f1
**Render Pipeline**: URP 2D (Universal Render Pipeline)
**Core Architecture**: Unity DOTS / ECS (Data-Oriented Technology Stack)
- `com.unity.entities` 1.3.14
- `com.unity.entities.graphics` 1.4.18
- `com.unity.burst` 1.8.21 — Burst-compiled Jobs for performance
- `com.unity.mathematics` 1.3.2
- `com.unity.collections` 2.5.3
**Input**: Unity Input System 1.18.0 (new input system, not legacy)

### Why DOTS/ECS

Vampire Survivors spawns hundreds of enemies and projectiles simultaneously.
DOTS gives us cache-friendly data, multithreaded Jobs, and Burst compilation —
critical for maintaining 60fps with 500+ entities on screen.

### Code Conventions

- Game logic lives in **ECS Systems** (not MonoBehaviours)
- Data lives in **IComponentData structs** (not classes)
- MonoBehaviours are only used for scene bootstrapping and UI
- Authoring components (`Baker`) convert scene GameObjects → ECS entities at bake time
- Prefer `IJobEntity` / `SystemAPI` over manual EntityQuery where possible

---

## Co-op Design Decisions

> These are our deviations from the original. The original game is single-player only.

### Scope: 2–4 Players, Local + Online

- **Local co-op**: split input, shared screen (camera tracks centroid of all players)
- **Online co-op**: target Netcode for Entities (server-authoritative)
- Decision: start with **local co-op first**, add online later

### Shared vs. Independent Systems

| System | Design |
|--------|--------|
| XP / Leveling | Each player levels independently |
| Gold / Treasure | Shared pool — any player pickup adds to team |
| Weapon selection on level-up | Each player gets their own level-up choice |
| Lives / Death | Player can be revived by teammates (hold interact near downed player) |
| Camera | Single camera, follows centroid, zooms out as players spread |
| Enemy aggro | Enemies target nearest player |
| Boss health | Scaled by player count (×1.5 per additional player) |

### Intentional Deviations from Original

- No "single character run" restriction — all players choose independently
- Revive mechanic added (does not exist in original)
- Enemy scaling adjusted for multi-player balance (see Boss health above)
- Camera is dynamic zoom rather than fixed follow

---

## Implementation Status

### Core Systems

- [x] Project initialized — DOTS/ECS, URP 2D, Input System
- [x] ECSBootstrap proof-of-concept (disabled — replaced by PlayerAuthoring)
- [x] Player entity — PlayerAuthoring bakes PlayerTag, MoveInput, MoveSpeed, PlayerStats, Health, Invincible, WeaponState
- [x] Input routing — WASD/Gamepad dev fallback; AssignedDeviceId stamped by GameSceneBootstrap from lobby
- [x] Camera system — CameraFollow (centroid lerp + dynamic zoom)
- [x] Enemy entity + movement AI — EnemyMovementSystem, Burst, chases nearest player
- [x] Enemy spawner — EnemySpawnerSystem, 3s waves, 5-8 burst, r=12 from centroid
- [x] Weapon system (Whip) — WhipSystem + HitArcSystem, 120° arc, 10 dmg, 0.5s CD
- [x] Health + damage loop — Health/Invincible components, ContactDamageSystem, HealthSystem
- [x] Main menu — Splash → PressToStart → Lobby (4-player device assignment, char cycling)
- [x] XP orb + leveling system — XpGem component, XpGemSystem (magnet r=30, collect r=0.5, speed=8), LevelUpSystem (wiki formula 5+(level-1)*10, 2s iframes)
- [x] Wave scaling — EnemySpawnerSystem: new wave every 30s, StatMultiplier +20%/wave (cap 3×), spawn count grows, interval shrinks to 1.5s floor, bat→skeleton weight shift
- [x] Level-up UI (upgrade choice cards) — HUDManager detects UpgradeChoicePending on player, pauses Time.timeScale, shows 3-button overlay; player picks Spinach/Pummarola/Armor; EntityManager applies stat + removes component; time resumes
- [ ] Pickup system (gold, health, magnets)
- [ ] Player death + revive mechanic — Downed component + state added; revive interaction is future work
- [x] HUD (per-player HP bars, XP bars, level text, timer) — HUDManager + HUDCanvas in 4_SampleScene
- [x] Magic Wand weapon — MagicWandSystem + Projectile component + ProjectileMovementSystem + ProjectileHitSystem
- [x] Fire Wand weapon — FireWandSystem, random-direction Projectile, per-player RNG
- [x] Game over screen — GameOverPanel overlay, "GAME OVER" + survived time; HUDManager detects all-Downed

### Weapons (clone priority order)

- [x] Whip — WhipSystem/HitArcSystem, hardcoded to all players
- [x] Magic Wand — MagicWandSystem fires Projectile at nearest enemy; 10 dmg, 0.5s CD, speed=10, range=15
- [x] Garlic — GarlicSystem aura pulse; 10 dmg, r=1.5, 1.5s CD, hits all enemies simultaneously
- [x] King Bible — KingBibleSystem; orbiting entity (KingBibleOrbit), radius=1.4u, 120°/s, 10 dmg, 0.5s hit CD; unlocked at lv5
- [x] Knife — KnifeSystem fires in FacingDirection (last movement dir); 10 dmg, 0.35s CD, speed=15
- [x] Fire Wand — FireWandSystem fires Projectile in random direction; 10 dmg, 0.4s CD, speed=11; per-player RNG via Unity.Mathematics.Random
- [x] Axe — AxeSystem; parabolic arc via Projectile.Gravity+Velocity; 20 dmg, 1.25s CD, ~60° elevation, Gravity=12 u/s²; unlocked at lv7
- [x] Cross — CrossSystem; returning boomerang via Projectile.TurnDistance+Returning+OwnerEntity; 50 dmg, 5.0s CD, speed=15, turn=8u; unlocked lv8
- [x] Holy Water — HolyWaterSystem throws flask (random dir, lands at 4u); HolyWaterPuddleSystem ticks all enemies in 1.5u radius every 0.5s for 5s; 20 dmg/tick; unlocked lv9
- [x] Lightning Ring — LightningRingSystem; instant hit-scan, picks Amount random enemies per Cooldown; 40 dmg, 0.6s CD, Amount=1; unlocked lv10

### Characters

- [ ] Antonio (starter — Whip)
- [ ] Imelda (starter — Magic Wand)
- [ ] Pasqualina (starter — Runetracer)
- [ ] Gennaro (starter — Knife)
- [ ] *(others later)*

### Enemies

- [x] Bat — red quad, HP=10, speed=2.5, dmg=10, XP=1; 60% spawn weight
- [x] Zombie — green quad, HP=40, speed=1, dmg=20, XP=5; 25% spawn weight
- [x] Skeleton — blue quad, HP=75, speed=1.8, dmg=25, XP=10; 15% spawn weight
- [ ] Slime (splits on death)
- [ ] *(bosses after basics work)*

### Passive Items / Stats

- [x] Spinach — PlayerStats.Might +0.1 per odd level (5,7,9…); all weapon systems multiply base damage by Might
- [x] Pummarola — PlayerStats.HpRegen +0.2 HP/s per even level (6,8,10…); HpRegenSystem (Burst, fractional accumulator)
- [x] Armor — PlayerStats.Armor int; ContactDamageSystem applies max(1, contactDamage - Armor); granted via level-up choice UI at lv11+ (Armor choice = index 2)
- [ ] Empty Tome (Cooldown -8%)
- [ ] *(others later)*

---

## Key File Paths

| File | Purpose |
|------|---------|
| `Assets/Scripts/ECSBootstrap.cs` | Proof-of-concept entity spawner — replace with real systems |
| `Assets/Scenes/SampleScene.unity` | Main test scene |
| `Assets/InputSystem_Actions.inputactions` | Input bindings (Move, Look, Attack, Interact, Sprint…) |
| `Assets/Settings/Renderer2D.asset` | URP 2D renderer config |
| `Assets/Settings/UniversalRP.asset` | URP global config |

---

## Reference

- **Original game wiki** (stats, formulas, weapon tables, enemy HP, etc.): https://vampire.survivors.wiki/
  - Use this for exact numbers when implementing any mechanic 1:1
- **Unity DOTS docs**: https://docs.unity3d.com/Packages/com.unity.entities@1.3/manual/index.html
- **Input System docs**: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.8/manual/index.html

---

## Development Notes

- Always check the wiki for exact values before hardcoding stats (damage, cooldown, speed, HP)
- ECSBootstrap.cs is throwaway — don't build on top of it
- When adding a new weapon or enemy, follow the Authoring + Baker pattern for scene integration
- Keep Systems focused: one responsibility per System
- Use `[BurstCompile]` on all Jobs and Systems unless they call managed APIs
