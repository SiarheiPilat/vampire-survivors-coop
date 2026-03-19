# XP Gems & Leveling — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enemies drop XP gems on death; players collect them by proximity; XP fills up and levels the player, granting brief iframes.

**Architecture:** New `XpGem` IComponentData component. `HealthSystem` modified to spawn gem entities at enemy death positions. Two new Burst/managed systems handle movement + collection and level-up logic.

**Tech Stack:** Unity Entities 1.3.14, Burst 1.8.21, Unity.Mathematics 1.3.2

---

## File Map

| Action | File |
|--------|------|
| **Modify** | `Assets/Scripts/Components/EnemyComponents.cs` — add `XpGem` struct |
| **Modify** | `Assets/Scripts/Authoring/PlayerAuthoring.cs` — XpToNextLevel: 100→5 |
| **Modify** | `Assets/Scripts/Systems/HealthSystem.cs` — spawn XpGem entity on enemy death |
| **Create** | `Assets/Scripts/Systems/XpGemSystem.cs` |
| **Create** | `Assets/Scripts/Systems/LevelUpSystem.cs` |

---

## Task 1: Add XpGem component + fix PlayerAuthoring XP start value

**Files:**
- Modify: `Assets/Scripts/Components/EnemyComponents.cs`
- Modify: `Assets/Scripts/Authoring/PlayerAuthoring.cs`

### Step 1: Add XpGem struct to EnemyComponents.cs

Inside `EnemyComponents.cs`, after the `SpawnerData` struct (at the end of the file, before the closing `}`), add:

```csharp
    /// <summary>
    /// Marks an XP gem entity. Spawned at enemy death positions by HealthSystem.
    /// XpGemSystem moves gems toward players in magnet radius and collects them on contact.
    /// </summary>
    public struct XpGem : IComponentData
    {
        public float Value;
    }
```

### Step 2: Fix XpToNextLevel in PlayerAuthoring.cs

In `PlayerAuthoring.cs` Baker, change `XpToNextLevel = 100f` to `XpToNextLevel = 5f`:

```csharp
AddComponent(entity, new PlayerStats
{
    Hp            = authoring.maxHp,
    MaxHp         = authoring.maxHp,
    Level         = 1,
    Xp            = 0f,
    XpToNextLevel = 5f   // ← was 100f; wiki: level 1→2 costs 5 XP
});
```

- [ ] **Step 1: Add XpGem struct to EnemyComponents.cs** (edit file — add the struct before the last `}`)

- [ ] **Step 2: Fix XpToNextLevel in PlayerAuthoring.cs** (change `100f` to `5f`)

- [ ] **Step 3: Verify compilation** — check Unity console, no errors expected

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Components/EnemyComponents.cs Assets/Scripts/Authoring/PlayerAuthoring.cs
git commit -m "feat: add XpGem component; fix XpToNextLevel starting value (100→5 per wiki)"
```

---

## Task 2: Modify HealthSystem to spawn XP gems on enemy death

**Files:**
- Modify: `Assets/Scripts/Systems/HealthSystem.cs`

When `HealthSystem` destroys an enemy entity (one with `EnemyTag`), it must also spawn an `XpGem` entity at the same position via ECB.

The modified `HealthSystem.OnUpdate` must:
1. Add `ComponentLookup<EnemyStats>` (ReadOnly) — initialized in `OnCreate`, updated in `OnUpdate`
2. Add `ComponentLookup<LocalTransform>` (ReadOnly) — initialized in `OnCreate`, updated in `OnUpdate`
3. After `if (health.ValueRO.Current > 0) continue;`, check for `EnemyTag` first, then spawn gem

Full modified file:

```csharp
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Runs after all damage systems. Destroys any entity whose Health.Current
    /// has dropped to or below 0. Logs a message for player deaths.
    /// Spawns an XpGem entity at the position of each destroyed enemy.
    /// Not Burst-compiled — calls Debug.Log.
    /// </summary>
    [UpdateAfter(typeof(ContactDamageSystem))]
    [UpdateAfter(typeof(HitArcSystem))]
    public partial struct HealthSystem : ISystem
    {
        ComponentLookup<EnemyStats>    _enemyStatsLookup;
        ComponentLookup<LocalTransform> _transformLookup;

        public void OnCreate(ref SystemState state)
        {
            _enemyStatsLookup = state.GetComponentLookup<EnemyStats>(isReadOnly: true);
            _transformLookup  = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _enemyStatsLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (health, entity) in
                SystemAPI.Query<RefRO<Health>>().WithEntityAccess())
            {
                if (health.ValueRO.Current > 0) continue;

                if (SystemAPI.HasComponent<EnemyTag>(entity))
                {
                    // Spawn XP gem at enemy's current position
                    if (_enemyStatsLookup.HasComponent(entity) && _transformLookup.HasComponent(entity))
                    {
                        var stats     = _enemyStatsLookup[entity];
                        var transform = _transformLookup[entity];

                        var gemEntity = ecb.CreateEntity();
                        ecb.AddComponent(gemEntity, new XpGem { Value = stats.XpValue });
                        ecb.AddComponent(gemEntity, LocalTransform.FromPosition(transform.Position));
                    }
                }
                else if (SystemAPI.HasComponent<PlayerTag>(entity))
                {
                    var idx = SystemAPI.GetComponent<PlayerIndex>(entity);
                    Debug.Log($"[HealthSystem] Player {idx.Value} died.");
                }

                ecb.DestroyEntity(entity);
            }
        }
    }
}
```

- [ ] **Step 1: Replace HealthSystem.cs** with the full content above

- [ ] **Step 2: Verify compilation** — check Unity console, no errors expected

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Systems/HealthSystem.cs
git commit -m "feat: HealthSystem spawns XpGem entity at enemy death position"
```

---

## Task 3: XpGemSystem — move and collect gems

**Files:**
- Create: `Assets/Scripts/Systems/XpGemSystem.cs`

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Moves XpGem entities toward the nearest player within MagnetRadius (30 units).
    /// When a gem reaches CollectRadius (0.5 units) of a player, it is absorbed:
    /// XP is added to the player's PlayerStats.Xp and the gem entity is destroyed.
    /// Runs single-threaded to avoid write races on shared PlayerStats.
    /// </summary>
    [BurstCompile]
    public partial struct XpGemSystem : ISystem
    {
        ComponentLookup<PlayerStats> _statsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _statsLookup = state.GetComponentLookup<PlayerStats>(isReadOnly: false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _statsLookup.Update(ref state);

            var playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag, LocalTransform, PlayerStats>()
                .Build();

            if (playerQuery.IsEmpty) return;

            var playerEntities   = playerQuery.ToEntityArray(Allocator.Temp);
            var playerTransforms = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            new CollectGemJob
            {
                PlayerEntities   = playerEntities,
                PlayerTransforms = playerTransforms,
                StatsLookup      = _statsLookup,
                Ecb              = ecb,
                DeltaTime        = SystemAPI.Time.DeltaTime
            }.Run();

            playerEntities.Dispose();
            playerTransforms.Dispose();
        }

        [BurstCompile]
        partial struct CollectGemJob : IJobEntity
        {
            const float MagnetRadius  = 30f;
            const float CollectRadius = 0.5f;
            const float GemSpeed      = 8f;

            [ReadOnly] public NativeArray<Entity>         PlayerEntities;
            [ReadOnly] public NativeArray<LocalTransform> PlayerTransforms;

            [NativeDisableParallelForRestriction] public ComponentLookup<PlayerStats> StatsLookup;

            public EntityCommandBuffer Ecb;
            public float DeltaTime;

            void Execute(Entity entity, ref LocalTransform transform, in XpGem gem)
            {
                // Find nearest player within MagnetRadius
                int   nearestIdx  = -1;
                float nearestDist = float.MaxValue;

                for (int i = 0; i < PlayerEntities.Length; i++)
                {
                    float dist = math.distance(transform.Position.xy, PlayerTransforms[i].Position.xy);
                    if (dist < MagnetRadius && dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestIdx  = i;
                    }
                }

                if (nearestIdx < 0) return; // no player in range

                if (nearestDist <= CollectRadius)
                {
                    // Collect: add XP, destroy gem
                    var stats = StatsLookup[PlayerEntities[nearestIdx]];
                    stats.Xp += gem.Value;
                    StatsLookup[PlayerEntities[nearestIdx]] = stats;
                    Ecb.DestroyEntity(entity);
                }
                else
                {
                    // Move toward player
                    float3 dir     = math.normalizesafe(PlayerTransforms[nearestIdx].Position - transform.Position);
                    float3 move    = dir * GemSpeed * DeltaTime;
                    transform.Position += new float3(move.x, move.y, 0f);
                }
            }
        }
    }
}
```

- [ ] **Step 1: Create XpGemSystem.cs** with the content above

- [ ] **Step 2: Verify compilation** — no errors expected

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Systems/XpGemSystem.cs Assets/Scripts/Systems/XpGemSystem.cs.meta
git commit -m "feat: add XpGemSystem (magnet + collect XP gems)"
```

---

## Task 4: LevelUpSystem — detect level-up threshold, grant iframes

**Files:**
- Create: `Assets/Scripts/Systems/LevelUpSystem.cs`

```csharp
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Checks each player's PlayerStats.Xp against XpToNextLevel.
    /// When threshold is met, increments Level, resets Xp, updates XpToNextLevel,
    /// and grants 2 seconds of invincibility.
    /// Level-up UI (weapon choice) is future work.
    /// Not Burst-compiled — calls Debug.Log.
    /// </summary>
    [UpdateAfter(typeof(XpGemSystem))]
    public partial struct LevelUpSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (stats, invincible, entity) in
                SystemAPI.Query<RefRW<PlayerStats>, RefRW<Invincible>>()
                    .WithAll<PlayerTag>().WithEntityAccess())
            {
                while (stats.ValueRO.Xp >= stats.ValueRO.XpToNextLevel)
                {
                    stats.ValueRW.Xp           -= stats.ValueRO.XpToNextLevel;
                    stats.ValueRW.Level         += 1;
                    // Wiki formula: XP to next level = 5 + (level-1) * 10, for levels 1-20
                    stats.ValueRW.XpToNextLevel  = 5f + (stats.ValueRO.Level - 1) * 10f;

                    // Brief invincibility on level-up
                    invincible.ValueRW.Timer = math.max(invincible.ValueRO.Timer, 2f);

                    var idx = SystemAPI.GetComponent<PlayerIndex>(entity);
                    Debug.Log($"[LevelUpSystem] Player {idx.Value} reached level {stats.ValueRO.Level}! Next level: {stats.ValueRO.XpToNextLevel} XP");
                }
            }
        }
    }
}
```

- [ ] **Step 1: Create LevelUpSystem.cs** with the content above

- [ ] **Step 2: Verify compilation** — no errors expected

- [ ] **Step 3: Enter play mode, kill enemies, verify level-up log appears**

Expected console output after ~5 Bat kills:
```
[LevelUpSystem] Player 0 reached level 2! Next level: 15 XP
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Systems/LevelUpSystem.cs Assets/Scripts/Systems/LevelUpSystem.cs.meta
git commit -m "feat: add LevelUpSystem (XP threshold → level up, iframes, wiki formula)"
```

---

## Integration Verification

Enter play mode in `4_SampleScene`:

1. **Gems spawn** — when a bat (hp=10) is killed by the whip, an XP gem entity appears at the bat's position (visible in DOTS Entities Hierarchy window)
2. **Gem attraction** — walking within 30 units of the gem causes it to move toward the player
3. **Gem collection** — gem is destroyed when it reaches the player; `PlayerStats.Xp` increases (visible in entity inspector)
4. **Level up** — after 5 bat kills, console logs `[LevelUpSystem] Player 0 reached level 2!`
5. **XpToNextLevel progression** — next threshold is 15, then 25 (per wiki formula)
6. **No errors** — console clean throughout
