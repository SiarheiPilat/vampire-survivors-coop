# Vampire Survivors Co-op — TODO

## Session Notes (2026-03-19)

- [ ] **Delete the one-shot editor script** (`Assets/Scripts/Editor/CreatePlayerSubScene.cs`)
      once the SubScene setup is stable — it's not needed at runtime.
- [ ] **MeshCollider on player Quads** — Quad primitive adds a MeshCollider automatically.
      Remove it from Player_0–3 (players don't need physics colliders yet).
- [ ] **HUD scene setup** — `HUDManager.cs` is created and committed. Needs Canvas + panel
      hierarchy built in `4_SampleScene` via Unity Editor. Plan: `docs/superpowers/plans/2026-03-19-hud.md`
      *(Unity MCP lost connection during long Burst compilation — do this when editor is responsive)*

## Menu / Lobby Follow-ups

- [ ] **CharacterRegistry ScriptableObject** — replace the hard-coded `Characters[]` array in
      `LobbyManager` with a proper registry asset that holds name, sprite, starting weapon.
- [ ] **Real customization/skin data** — `PlayerSlotUI` currently shows a slot index; wire up
      actual skin sprites/names when art assets exist.
- [ ] **Apple TV Remote support** — tvOS Siri Remote shows up as a non-Gamepad `InputDevice`.
      `LobbyManager.Update()` skips non-Gamepad devices; add MFi / Apple TV remote handling.
- [ ] **Back-navigation from Lobby** — pressing B/Circle with no joined players should return
      to PressToStartScene instead of doing nothing.

---

## Core Systems

- [x] Project initialized — DOTS/ECS, URP 2D, Input System
- [x] ECSBootstrap proof-of-concept (disabled — replaced by PlayerAuthoring)
- [x] Player entity — PlayerAuthoring, MoveInput, MoveSpeed, PlayerStats, PlayerTag, PlayerIndex
- [x] Input routing — WASD/Gamepad dev fallback; lobby assigns devices by button-press (AssignedDeviceId)
- [x] Camera system — centroid follow + dynamic zoom (CameraFollow)
- [x] SubScene setup — Player_0–3 baked into ECS entities via Players.unity SubScene
- [x] Enemy entity + movement AI — EnemyMovementSystem chases nearest player (Burst)
- [x] Enemy spawner — EnemySpawnerSystem, 3s waves, Bat/Zombie/Skeleton weighted random at r=12
- [x] Weapon system (Whip) — WhipSystem + HitArcSystem, 120° arc, 0.5s cooldown, 10 dmg
- [x] Health + damage loop — Health/Invincible components, ContactDamageSystem, HealthSystem
- [x] XP orb + leveling system — XpGem, XpGemSystem (magnet r=30, speed=8, collect r=0.5), LevelUpSystem (wiki formula, 2s iframes)
- [ ] Level-up UI (weapon choice cards)
- [ ] Pickup system (gold, health, magnets)
- [ ] Player death + revive mechanic
- [ ] HUD (per-player HP bars, timer, kill count)
- [x] Main menu / character select — Splash → PressToStart → Lobby (4-player device assignment, char cycling, persistence)
- [ ] Game over / win screen

---

## Weapons (priority order)

- [x] Whip — WhipSystem/HitArcSystem, 120°arc, 10dmg, 0.5s CD, 1.5 range
- [ ] Magic Wand
- [ ] Garlic
- [ ] King Bible
- [ ] Fire Wand
- [ ] Knife
- [ ] Axe
- [ ] Cross
- [ ] Holy Water
- [ ] Lightning Ring

---

## Characters

- [ ] Antonio (starter — Whip)
- [ ] Imelda (starter — Magic Wand)
- [ ] Pasqualina (starter — Runetracer)
- [ ] Gennaro (starter — Knife)

---

## Enemies

- [x] Bat — hp=10, spd=2.5, dmg=10, xp=1 (red quad)
- [x] Zombie — hp=40, spd=1.0, dmg=20, xp=5 (green quad)
- [x] Skeleton — hp=75, spd=1.8, dmg=25, xp=10 (blue quad)
- [ ] Slime (splits on death)
- [ ] Bosses (after basics work)

---

## Passive Items / Stats

- [ ] Spinach (Might +10%)
- [ ] Armor (reduce incoming dmg)
- [ ] Pummarola (HP regen)
- [ ] Empty Tome (Cooldown -8%)

---

## Co-op Specific

- [ ] Shared gold pool (any player pickup adds to team)
- [ ] Independent XP / leveling per player
- [ ] Per-player level-up weapon choice
- [ ] Player death + revive (hold interact near downed player)
- [x] Enemy aggro — EnemyMovementSystem targets nearest player (Burst, XY-plane constrained)
- [ ] Boss HP scaling (×1.5 per additional player)
- [x] "Press button to join" device-assignment lobby — implemented in LobbyScene
- [ ] Online co-op via Netcode for Entities (after local co-op is solid)
