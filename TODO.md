# Vampire Survivors Co-op — TODO

## Session Notes (2026-03-19)

- [ ] **Delete the one-shot editor script** (`Assets/Scripts/Editor/CreatePlayerSubScene.cs`)
      once the SubScene setup is stable — it's not needed at runtime.
- [ ] **MeshCollider on player Quads** — Quad primitive adds a MeshCollider automatically.
      Remove it from Player_0–3 (players don't need physics colliders yet).

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
- [ ] Enemy entity + movement AI (target nearest player)
- [ ] Enemy spawner (wave definitions, spawn rates)
- [ ] Weapon system (auto-fire, projectile logic)
- [ ] XP orb + leveling system
- [ ] Level-up UI (weapon choice cards)
- [ ] Pickup system (gold, health, magnets)
- [ ] Player death + revive mechanic
- [ ] HUD (per-player HP bars, timer, kill count)
- [x] Main menu / character select — Splash → PressToStart → Lobby (4-player device assignment, char cycling, persistence)
- [ ] Game over / win screen

---

## Weapons (priority order)

- [ ] Whip
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

- [ ] Bat (basic — chases player)
- [ ] Zombie (slow melee)
- [ ] Skeleton (medium melee)
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
- [ ] Enemy aggro — target nearest player
- [ ] Boss HP scaling (×1.5 per additional player)
- [x] "Press button to join" device-assignment lobby — implemented in LobbyScene
- [ ] Online co-op via Netcode for Entities (after local co-op is solid)
