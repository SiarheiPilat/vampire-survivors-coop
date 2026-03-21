# Vampire Survivors Co-op — TODO

> Last updated: 2026-03-21 ~22:30

## Next Up (priority order)

- [ ] **HUD stat tooltip on level-up cards** — show per-card stat line (e.g. "Might: 1.1 → 1.2") so players can see what they're gaining
- [ ] **More characters** — Mortaccio, Yatta, Krochi, Dommario, Giovanna, Clerici, Poppea, Pugnala, Bi-An Zi are in `GameSceneBootstrap` but not in the Lobby character list yet; add to `LobbyManager.AllCharacterIds`

## Completed

### 2026-03-21 (Session 38 — ~22:30)

- [x] **Character per-level stat scaling** — 4 new `PlayerStats` fields: `MightBonusPerLevel`, `XpMultBonusPerLevel`, `AreaBonusPerLevel`, `CooldownBonusPerLevel`; `LevelUpSystem` applies all 6 bonus-per-level fields (existing: ProjectileSpeed, Duration; new: Might, XpMult, Area, Cooldown) every level-up; `GameSceneBootstrap` sets per-character values: Antonio +1% Might/lv, Imelda +1% XP/lv, Pasqualina +1% ProjSpeed/lv, Arca -1% CD/lv (floor 0.5), Porta +1% Area/lv, Lama +1% Might/lv; Giovanna/Poppea already had their fields set; wiki-accurate scaling

## Completed

### 2026-03-21 (Session 37 — ~22:15)

- [x] **Boss health bar** — `HUDManager` creates a dark-background full-width bar at the top (10–90% screen width, top 3–8%) via `CreateBossHpBar()`; red fill image + white HP text "BOSS  6450 / 10000"; `UpdateBossHpBar()` queries `_bossQuery` each frame for entities with `BossTag`/`EliteTag`/`DeathBossTag`, shows the one with highest `Health.Max`; hidden when no boss; label changes to "ELITE" / "DEATH" for those variants

### 2026-03-21 (Session 36 — ~22:00)

- [x] **Curse XP multiplier** — HealthSystem now computes `avgCurse` alongside `avgLuck` in the same player loop; XP gems spawned on enemy death use `enemyStats.XpValue × (1 + avgCurse × 0.1)`; wiki accurate: +10% XP per Curse point; risk/reward completes the Curse mechanic

### 2026-03-21 (Session 35 — ~21:45)

- [x] **Victory Death boss** — `DeathBossTag : IComponentData`; `DeathRegenSystem` +666 HP/s (`[UpdateBefore(HealthSystem)]`); `HealthSystem` uses `WithNone<DeathBossTag>()` to skip destruction; `HUDManager._deathSpawned` flag; `SpawnDeathBosses()` at 29:55 spawns one Death per living player at their position (scale 2.0, HP=666000, ContactDmg=666, no XP); reuses `SpawnerData.BossPrefab` for visuals; shows "DEATH APPROACHES" banner via `StageBanner.Show()`

### 2026-03-21 (Session 34 — ~21:30)

- [x] **Floor item magnet** — `GoldCoinSystem` rewritten: gold coins now slide toward nearest player at 6 u/s within `4u × MagnetRadiusMult`; `HealthPickupSystem` rewritten identically; both still collect at 0.6u; both add `PlayerStats` to their player queries to read `MagnetRadiusMult`; scales naturally with Attractorb passive; items idle until player is in range

### 2026-03-21 (Session 33 — ~21:15)

- [x] **Curse active effects** — `EnemyMovementSystem.OnUpdate` now non-Burst; computes team average Curse, passes `CurseSpeedMult = 1 + avgCurse × 0.1` to both movement jobs (all enemies move +10% faster per Curse point); `ContactDamageSystem.OnUpdate` similarly non-Burst; computes `CurseDamageMult`, scales `ContactDamage × CurseDamageMult` before Armor reduction (+10% contact damage per Curse point); job structs retain `[BurstCompile]`

### 2026-03-21 (Session 32 — ~21:00)

- [x] **Achievement display** — `AchievementHintPanel` MonoBehaviour (Menu assembly, auto-created via RuntimeInitializeOnLoadMethod in Lobby scene); scans all locked characters, picks the one with highest completion ratio; shows gold-tinted "Next unlock: <Name> — hint" strip at bottom of screen (sorting order 15, semi-transparent background); destroys itself when leaving Lobby scene; "All characters unlocked!" when none remain

### 2026-03-21 (Session 31 — ~20:45)

- [x] **Enemy elite variants** — `EliteTag : IComponentData` added; `EnemySpawnerSystem` promotes 2% of spawns to elite post-stat-scaling: HP×3, XP×2, Speed×1.15, Scale×1.35; `EliteTag` added to entity; `HealthSystem` guarantees Chest drop for elites
- [x] **Bomb pickup** — `BombPickup : IComponentData`; dropped by enemies at 1% base chance (Luck-scaled) via `HealthSystem`; `BombPickupSystem` (walk-over r=0.6u, deals 80 flat dmg to all `EnemyTag` in 3u radius, bypasses Armor, main-thread direct Health.Current write); `BombPickupPrefab` (orange-red quad 0.3u); `BombPickupMaterial.mat`; wired to `SpawnerAuthoring` in game scene; `PickupKind.BombPickup=6` baker case

### 2026-03-21 (Session 30 — ~09:00)

- [x] **Bracer/Candelabrador/Spellbinder passive gating** — `BuildUpgradeChoices` now computes `hasProjectileWeapon`, `hasAreaWeapon`, `hasDurationWeapon` booleans from player's ECS components; Bracer skipped if no projectile weapon; Candelabrador skipped if no area weapon; Spellbinder skipped if no duration weapon; SilverRing/GoldRing require ClockLancetState
- [x] **UnlockToast** — `VampireSurvivors.Menu.UnlockToast` class (Menu assembly); `CheckAndShow(registry)` compares current unlocks vs last saved set (PlayerPrefs "unlocked_chars_v1"); queued golden banner toasts: fade-in 0.3s / hold 2.0s / fade-out 0.4s; called from `LobbyManager.Start()`
- [x] **SettingsPanel Reset Progress button** — `resetProgressButton` Button field added; `OnResetProgress()` calls `PersistentProgress.ResetAll()` + clears unlock toast tracking; `ResetProgressButton` dark-red UI element added to SettingsPanel in LobbyScene, wired in Inspector

### 2026-03-21 (Session 29 — ~08:35)

- [x] **Lobby stage name UI** — `StageText` TMP_Text added to Lobby canvas (bottom row, golden tint); shows "Stage: Mad Forest   [ Q / E ]"; wired to `LobbyManager.stageNameText` in Inspector; `RefreshStageDisplay()` updates it on cycle
- [x] **HUD stage name banner** — `StageBanner` MonoBehaviour (auto-created, DontDestroyOnLoad); `StageBanner.Show(name)` creates Screen-Space-Overlay Canvas + TMP label; fade-in 0.4s, hold 1.6s, fade-out 0.5s; unscaled time; sorting order 200 (above everything); called from `GameSceneBootstrap.ApplyStage()`
- [x] **PersistentProgress** — `PersistentProgress.cs` static class; PlayerPrefs-backed: `TotalKills`, `TotalGold`, `BestSurviveMin`, `BestLevel`, `OrologionCount`; `IsUnlocked(charId)` gates 9 characters behind milestones (tier 1: always unlocked; tier 2: kills/gold/survive/level/orologion thresholds); `UnlockHint(charId)` returns progress string; `SaveRunStats()` + `IncrementOrologion()` + `ResetAll()`
- [x] **HUDManager persist on game end** — `SaveRunProgress()` reads ECS `SharedGold.EnemiesKilled + Total` + best `PlayerStats.Level`; calls `PersistentProgress.SaveRunStats()`; fires in both `TriggerGameOver` and `TriggerVictory`
- [x] **OrologionPickupSystem** — calls `PersistentProgress.IncrementOrologion()` on each collect
- [x] **LobbyManager unlock gating** — `CycleCharacter` skips locked characters; `JoinSlot` falls back to "antonio" if saved character is now locked

### 2026-03-21 (Session 28 — ~08:00)

- [x] **Stage system** — `StageDefinition` + `StageRegistry` ScriptableObjects; `GameSession.StageId` (default "mad_forest"); 3 stages: Mad Forest (dark green), Inlaid Library (dark blue/purple, more skeletons+specters starting wave 4), Dairy Plant (grey-blue, more zombies+ghouls starting wave 3); `StageRegistry.asset` pre-populated
- [x] **InfiniteBackground stage colors** — `Instance` singleton + `SetStageColors(a, b)` method; tile colors swapped dynamically when `GameSceneBootstrap.ApplyStage()` runs; no scene wiring needed
- [x] **GameSceneBootstrap stage wiring** — `[SerializeField] StageRegistry`; `ApplyStage()` pushes colors to `InfiniteBackground.Instance` and writes `StageIndex` to `SpawnerData` singleton; hardcoded fallback colors when registry not assigned
- [x] **EnemySpawnerSystem stage weights** — `SpawnerData.StageIndex`; Inlaid Library: lower bat base (40%), specter starts wave 4 (was 7), specter cap 14%; Dairy Plant: higher zombie base (30%), ghoul starts wave 3 (was 5)
- [x] **Lobby stage cycling** — Q/E keyboard + LB/RB gamepad (first joined player); `StageId` written to `GameSession` before scene load; `StageRegistry` wired to `LobbyManager` in Inspector; optional `stageNameText` TMP_Text field (null-safe)

### 2026-03-21 (Session 27 — ~07:40)

- [x] **Ghoul enemy** — hp=130, speed=1.8, contactDmg=25, xp=15; GhoulTag baked; dark-purple quad (0.45u); spawns wave 5+, 10% weight; GhoulMaterial assigned to prefab
- [x] **Specter enemy** — hp=80, speed=2.2, contactDmg=20, xp=12; GhostTag baked (knockback-immune via EnemyMovementSystem WithNone\<GhostTag\>); pale-blue quad (0.42u); spawns wave 7+, 8% weight; SpecterMaterial assigned; both wired to SpawnerAuthoring in game scene

### 2026-03-21 (Session 26 — ~07:10)

- [x] **Orologion floor item** — `OrologionPickup` IComponentData; `OrologionPickupSystem` (main thread, walk-over 0.6u radius) freezes ALL on-screen enemies for 10s via existing `Frozen` + `FrozenTickSystem`; 1.5% base drop chance on enemy death (scales with Luck); `OrologionPickupPrefab` white-blue quad (0.35u); `SpawnerData/SpawnerAuthoring` wired; `PickupKind.OrologionPickup` added; note: InfiniteCorridorEvolution gate (Clock Lancet + Silver Ring + Gold Ring) is already correct per wiki
- [x] **Score screen overhaul** — `BuildScoreScreen(panel, victory)` helper shared by TriggerGameOver + TriggerVictory; shows per-player row: "P1 — CharName  Lv X" (colored blue/green for dead/victory); reads PlayerStats.Level + PlayerIndex from ECS, CharacterId from GameSession; falls back to capitalized id if CharacterRegistry not assigned; team stats (kills, gold) follow with dynamic y-offset; `[SerializeField] CharacterRegistry characterRegistry` added to HUDManager

### 2026-03-21 (Session 25 — ~06:40)

- [x] **PeachoneState** — rotating CW egg weapon; 10 dmg, 1.4s CD, 6 u/s, 5u range, Amount 1 (upgradeable to 3); Angle advances +30°/cycle; auto-granted at level 16
- [x] **EbonyWingsState** — rotating CCW bat weapon (mirror stats, Angle starts at π, advances -30°/cycle); silent when IsEvolved; auto-granted at level 17
- [x] **PeachoneSystem** — fires Amount projectiles centered on Angle; when evolved (Vandalier) fires CW+CCW simultaneously at 15 dmg, 0.7s CD
- [x] **EbonyWingsSystem** — fires Amount projectiles CCW; ticks timer/advances Angle when evolved but spawns nothing
- [x] **Vandalier evolution** (Peachone + Ebony Wings, no passive gate) — `PeachoneState.IsEvolved=true, Damage=15, Cooldown=0.7s`; `EbonyWingsState.IsEvolved=true`; pool entry in HUDManager BuildUpgradeChoices
- [x] **Bi-An Zi character** — HP=100, Speed=7.0; both Peachone+EbonyWings as starters (EbonyWings Angle=π offset); added to GameSceneBootstrap + LobbyManager fallback + CharacterRegistry (index 15)
- [x] **HUDManager** — `PeachoneAmount`, `EbonyWingsAmount`, `VandalierEvolution` UpgradeType entries; Duplicator gives +1 to both Peachone and EbonyWings when not evolved
- [x] **LevelUpSystem** — case 16 (Peachone), case 17 (EbonyWings) with `!HasComponent` guards

### 2026-03-21 (Session 24 — ~06:10)

- [x] **CharacterRegistry ScriptableObject** — `CharacterDefinition` serializable class (Id, DisplayName, Description); `CharacterRegistry : ScriptableObject` with `Find(id)`, `GetDisplayName(id)`, `GetDescription(id)`, `IdAt(index)`, `Count`; `[CreateAssetMenu]` menu entry; `Assets/Data/CharacterRegistry.asset` pre-populated with all 15 characters (Antonio → Clerici) with display names + stat-summary descriptions
- [x] **LobbyManager** — replaced `static readonly string[] Characters` with `[SerializeField] CharacterRegistry characterRegistry` + `s_FallbackIds` fallback; `CharacterCount`/`IdAt()` helpers; `RefreshSlotDisplay(slot)` resolves display name + description from registry; `IndexOfId()` replaces `Array.IndexOf`; `CycleCharacter`/`CycleCustomization`/`JoinSlot` all use new helpers
- [x] **PlayerSlotUI** — `ShowJoined(string displayName, string description, int customizationIndex)` — now accepts pre-resolved display name (no manual capitalize); optional `[SerializeField] TMP_Text characterDescription` field for description line (null-safe)

## Completed

### 2026-03-21 (Session 23 — ~05:30)

- [x] **Metaglio Left passive** — `PlayerStats.MetaglioLeftStacks` int (max 9); each pickup: `HpRegen += 0.1f`, `Health.Max += 5% of current MaxHp`, `MaxHpBonus += bonus`; in passive upgrade pool (excluded when stacks ≥ 9 via pre-filter loop); gate for Crimson Shroud
- [x] **Metaglio Right passive** — `PlayerStats.MetaglioRightStacks` int (max 9); each pickup: `Curse += 0.05f`; in passive upgrade pool (excluded when stacks ≥ 9); gate for Crimson Shroud
- [x] **Crimson Shroud evolution** (Laurel + Metaglio Left + Metaglio Right) — `LaurelState.IsEvolved=true`; `MaxDamageCap=10` (ContactDamageSystem clamps per-hit damage ≤ 10); `Cooldown=8.0s`; `RetaliationDamage=30`, `RetaliationRadius=2.0u` (LaurelSystem fires AoE retaliation each pulse when evolved)
- [x] **ContactDamageSystem** — added `ComponentLookup<LaurelState>` (read-only, Burst-compatible); clamps damage at `MaxDamageCap` when player has evolved Laurel
- [x] **LaurelSystem** rewritten — now Burst IJobEntity `.Run()` like GarlicSystem; adds `ComponentLookup<Health>` + enemy arrays for evolved AoE pulse; non-evolved path unchanged (just sets Invincible.Timer)
- [x] **PlayerAuthoring** — `MetaglioLeftStacks=0`, `MetaglioRightStacks=0` defaults baked

### 2026-03-21 (Session 22 — ~05:10)

- [x] **Laurel weapon** (`LaurelState` + `LaurelSystem`) — 10.0s CD, 0.5s InvulDuration; every Cooldown×CooldownMult seconds sets player `Invincible.Timer = InvulDuration×DurationMult`; integrates with existing ContactDamageSystem (already skips invincible players); Burst-compiled; auto-granted at level 15; no Amount/damage upgrade (wiki: only benefits from Cooldown); Crimson Shroud evolution deferred (needs Metaglio L+R)
- [x] **Santa Clerici** character — Holy Water starter (same stats as Yatta Cavallo), HP=150 (+50 wiki), HpRegen=0.5/s (+0.5 Recovery wiki), Speed=7.0; added to LobbyManager roster

### 2026-03-21 (Session 21 — ~04:50)

- [x] **Song of Mana weapon** (`SongOfManaState` + `SongOfManaSystem`) — 10 dmg, 2.0s CD; rectangular vertical column (1.5u wide × 6.0u tall half-extents: 0.75×3.0); ignores ProjectileSpeedMult (wiki); scales with AreaMult; Burst-compiled IJobEntity (.Run(), single-threaded); horizontal knockback 3 u/s; auto-granted at level 14; Poppea starts with it
- [x] **Mannajja evolution** (Song of Mana + Skull O'Maniac) — `SongOfManaState.IsEvolved=true`; 40 dmg, 4.5s CD, HalfWidth=3.0u (6u total), HalfHeight=4.0u (8u total); gate: has SongOfManaState + !IsEvolved + SkullOManiacStacks > 0
- [x] **Skull O'Maniac passive** — `PlayerStats.SkullOManiacStacks` int + `Curse += 0.1f` per pickup (5 max × wiki); in upgrade pool: "+10% Curse (enemies hit harder, drop more XP)"
- [x] **Poppea Pecorina** character — Song of Mana starter, HP=100, Speed=8.4 (+20%), `DurationBonusPerLevel=0.01f` (+1% DurationMult per level, no cap); added to LobbyManager roster; `DurationBonusPerLevel` field in PlayerStats + PlayerAuthoring; LevelUpSystem applies it additively each level-up

### 2026-03-21 (Session 20 — ~04:30)

- [x] **Eight The Sparrow weapon** (`EightSparrowState` + `EightSparrowSystem`) — 5 dmg, 1.4s CD, speed=12, MaxRange=12; fires Amount bullets in 4 diagonal directions (45°/135°/225°/315°) each Cooldown; spread ±8°/bullet when Amount>1; `EightAmount` in upgrade pool (cap 3); auto-granted at level 13; Pugnala starts with it
- [x] **Phieraggi evolution** (Phiera + Eight + Tiragisú) — `PhieraState.IsEvolved=true` + `EightSparrowState.IsEvolved=true`; PhieraSystem fires 8 directions at 0.35s CD; EightSparrowSystem skips when IsEvolved; gate: has both states + HasComponent<ReviveStocks>; Duplicator applies to both weapons
- [x] **Pugnala Provola** character — HP=100, Speed=7.4; twin pistols (Phiera + Eight) both as starters; added to LobbyManager roster

### 2026-03-21 (Session 18 — ~04:10)

- [x] **Frozen IComponentData** — `Frozen { float Timer }` in EnemyComponents.cs; while present, enemy is immobilised (no movement); weapons still deal damage; `EnemyMovementSystem` both jobs now `[WithNone(Frozen)]`
- [x] **ClockLancetState** — Cooldown=2.0s, FreezeRadius=6.0u, FreezeDuration=2.0s; auto-granted at level 11 via `LevelUpSystem` (guard: `!HasComponent<ClockLancetState>`)
- [x] **ClockLancetSystem** — non-Burst; every Cooldown freezes all enemies within 6u; `ComponentLookup<Frozen>` refreshes already-frozen timers in place; ECB adds `Frozen` to new targets; respects CooldownMult + DurationMult
- [x] **FrozenTickSystem** — Burst ISystem; decrements `Frozen.Timer` each frame; removes via ECB on expire; runs `[UpdateBefore(EnemyMovementSystem)]`
- [x] **Tiragisú passive** — `+1 ReviveStocks` per upgrade choice pickup; adds `ReviveStocks` component if player doesn't have one (universal, all characters); appears in upgrade pool

### 2026-03-21 (Session 17 — ~03:50)

- [x] **Stone Mask passive** — `PlayerStats.GoldMult` +0.1 per pickup (additive, wiki: +10% Greed/level); `GoldCoinSystem` tracks nearest player index, applies `coin.Value × GoldMult` (rounded); default 1.0
- [x] **Vicious Hunger evolution** (Gatti Amari + Stone Mask) — `GattiAmariState.IsEvolved=true`; 30 dmg, 8.0s CD, 2 giant cats (scale=0.6u), 7.0s lifetime, 1.5u×AreaMult AoE radius; `GattiAmariSystem` branches on `IsEvolved`; `GattiAmariAmount` + Duplicator blocked when evolved; gate: `GoldMult > 1.0`

### 2026-03-21 (Session 16 — ~03:30)

- [x] **Giovanna Grana** character — Gatti Amari starter, HP=100, Speed=8.4 (+20%), `ProjectileSpeedBonusPerLevel=0.01f` (+1% ProjSpeed per level); added to LobbyManager roster
- [x] **Gatti Amari weapon** (`GattiAmariState` + `GattiAmariCat` + `GattiAmariSystem` + `GattiAmariCatSystem`) — cats spawn at player pos (5s CD, Amount=1); wander at 1.5 u/s, change dir every ~0.75s; attack all enemies in 0.5u×AreaMult radius every 1s (10 dmg×Might); expire after 5s×DurationMult; `GattiAmariAmount` in upgrade pool (cap 3); Duplicator applies
- [x] **LevelUpSystem** — applies `ProjectileSpeedBonusPerLevel` additively to `ProjectileSpeedMult` each level-up (0.0 for all chars except Giovanna's 0.01)

### 2026-03-21 (Session 15 — ~03:00)

- [x] **Hellfire evolution** (Fire Wand + Spinach) — `FireWandState.IsHellfire=true`; 100 dmg, speed=1.5 u/s, 2 meteors/volley, MaxRange=25u, 3.0s CD, `Piercing=true`, random direction; `FireWandSystem` branches on `IsHellfire` (larger 0.45u visual scale); gate: `Might > 1.0`; FireAmount + Duplicator Fire increment blocked when IsHellfire; coexists with O'Sole Meeo (uses separate bool)
- [x] **La Borra evolution** (Holy Water + Attractorb) — `HolyWaterState.IsEvolved=true`; 40 dmg/tick, 4.0s CD, 4 flasks/volley, radius=3.0u (200%), lifetime=4.0s; `HolyWaterPuddle.FollowsPlayer=true` set via flask → puddle chain; `HolyWaterPuddleSystem` creeps following puddles toward nearest player at 2 u/s; gate: `MagnetRadiusMult > 1.0`; HolyWaterAmount + Duplicator blocked when evolved

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
