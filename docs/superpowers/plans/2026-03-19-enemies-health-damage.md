# Enemies, Health, Damage & Whip — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** First playable combat loop — enemies spawn, chase players, deal contact damage, and die when hit by the Whip weapon.

**Architecture:** Pure ECS/DOTS. New components in `EnemyComponents.cs`. Six new systems. Two new Authoring MonoBehaviours baked via Unity's Baker. Three enemy prefabs (Bat, Zombie, Skeleton) placed via Unity MCP. Whip is hardcoded to all player entities via PlayerAuthoring.

**Tech Stack:** Unity Entities 1.3.14, Burst 1.8.21, Unity.Mathematics 1.3.2, Unity.Collections 2.5.3

---

## File Map

| Action | File |
|--------|------|
| **Create** | `Assets/Scripts/Components/EnemyComponents.cs` |
| **Modify** | `Assets/Scripts/Authoring/PlayerAuthoring.cs` |
| **Create** | `Assets/Scripts/Authoring/EnemyAuthoring.cs` |
| **Create** | `Assets/Scripts/Authoring/SpawnerAuthoring.cs` |
| **Create** | `Assets/Scripts/Systems/EnemyMovementSystem.cs` |
| **Create** | `Assets/Scripts/Systems/InvincibilitySystem.cs` |
| **Create** | `Assets/Scripts/Systems/ContactDamageSystem.cs` |
| **Create** | `Assets/Scripts/Systems/WhipSystem.cs` |
| **Create** | `Assets/Scripts/Systems/HitArcSystem.cs` |
| **Create** | `Assets/Scripts/Systems/HealthSystem.cs` |
| **Create** | `Assets/Scripts/Systems/EnemySpawnerSystem.cs` |
| **Unity** | `Assets/Prefabs/Enemies/Bat.prefab`, `Zombie.prefab`, `Skeleton.prefab` |
| **Unity** | `Assets/Scenes/4_SampleScene.unity` — add EnemySpawner GameObject |

---

## Task 1: EnemyComponents.cs — Define all new ECS components

**Files:**
- Create: `Assets/Scripts/Components/EnemyComponents.cs`

This file defines all new component types needed for the combat loop. Add to the existing `VampireSurvivors.Components` namespace. `Health` is shared by both enemies and players. `WeaponState` and `HitArc` are player/weapon components. `SpawnerData` holds prefab entity refs and spawner state.

- [ ] **Step 1: Create the component definitions**

```csharp
using Unity.Entities;
using Unity.Mathematics;

namespace VampireSurvivors.Components
{
    /// <summary>Zero-size marker — identifies all enemy entities.</summary>
    public struct EnemyTag : IComponentData { }

    /// <summary>Enemy movement and combat stats.</summary>
    public struct EnemyStats : IComponentData
    {
        public float MoveSpeed;
        public int ContactDamage;
        public int XpValue;
    }

    /// <summary>
    /// Current and maximum hit points. Added to both enemy and player entities.
    /// PlayerStats.Hp/MaxHp remain untouched (used for leveling later).
    /// </summary>
    public struct Health : IComponentData
    {
        public int Current;
        public int Max;
    }

    /// <summary>
    /// Contact-damage cooldown. When Timer > 0 the entity cannot take contact damage.
    /// Ticked down by InvincibilitySystem. Added to player entities only.
    /// </summary>
    public struct Invincible : IComponentData
    {
        public float Timer;
    }

    /// <summary>
    /// Whip weapon state on player entities. SwingTimer counts down; when it hits 0 a
    /// HitArc entity is spawned and the timer resets to SwingCooldown.
    /// </summary>
    public struct WeaponState : IComponentData
    {
        public float SwingTimer;
        public float SwingCooldown;
        public float Damage;
        public float Range;
        public float ArcDegrees;
    }

    /// <summary>
    /// Transient entity created by WhipSystem. Exists for one frame only.
    /// HitArcSystem reads it, applies damage to enemies in range+arc, then destroys it.
    /// Origin stores the player world position at swing time.
    /// </summary>
    public struct HitArc : IComponentData
    {
        public float Damage;
        public float2 Direction;   // normalised facing direction
        public float Range;
        public float ArcDegrees;
        public float3 Origin;      // world position of the swinging player
    }

    /// <summary>
    /// Singleton component on the spawner entity. Baked by SpawnerAuthoring.
    /// Holds entity prefab references and mutable spawner state (timer + RNG).
    /// </summary>
    public struct SpawnerData : IComponentData
    {
        public Entity BatPrefab;
        public Entity ZombiePrefab;
        public Entity SkeletonPrefab;
        public float Timer;
        public Unity.Mathematics.Random Rng;
    }
}
```

- [ ] **Step 2: Check compilation**

In Unity console, verify no errors. Expected: clean compile.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Components/EnemyComponents.cs Assets/Scripts/Components/EnemyComponents.cs.meta
git commit -m "feat: add EnemyComponents (EnemyTag, EnemyStats, Health, Invincible, WeaponState, HitArc, SpawnerData)"
```

---

## Task 2: PlayerAuthoring — bake Health + WeaponState onto player entities

**Files:**
- Modify: `Assets/Scripts/Authoring/PlayerAuthoring.cs`

Add `Health` and `WeaponState` to what the Baker stamps on each player entity. This means both lobby-entered and dev-mode-direct players get these components from bake time, requiring no runtime additions.

- [ ] **Step 1: Modify PlayerAuthoring.cs Baker**

Replace the Baker class in `Assets/Scripts/Authoring/PlayerAuthoring.cs`. Full file:

```csharp
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Authoring
{
    /// <summary>
    /// Add to a scene GameObject (alongside MeshFilter + MeshRenderer for visuals).
    /// Baker stamps custom ECS components onto the entity; Unity's built-in
    /// MeshRendererBaker handles the rendering components automatically.
    /// </summary>
    public class PlayerAuthoring : MonoBehaviour
    {
        [Header("Player Config")]
        public byte playerIndex;
        public float moveSpeed = 7f;
        public int maxHp = 100;

        class Baker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new PlayerTag());
                AddComponent(entity, new PlayerIndex { Value = authoring.playerIndex });
                AddComponent(entity, new MoveInput { Value = float2.zero });
                AddComponent(entity, new MoveSpeed { Value = authoring.moveSpeed });
                AddComponent(entity, new PlayerStats
                {
                    Hp            = authoring.maxHp,
                    MaxHp         = authoring.maxHp,
                    Level         = 1,
                    Xp            = 0f,
                    XpToNextLevel = 100f
                });
                AddComponent(entity, new AssignedDeviceId { Value = 0 });
                AddComponent(entity, new Health { Current = authoring.maxHp, Max = authoring.maxHp });
                AddComponent(entity, new Invincible { Timer = 0f });
                AddComponent(entity, new WeaponState
                {
                    SwingTimer    = 0f,
                    SwingCooldown = 0.5f,
                    Damage        = 10f,
                    Range         = 1.5f,
                    ArcDegrees    = 120f
                });
            }
        }
    }
}
```

- [ ] **Step 2: Check compilation**

In Unity console, verify no errors. Expected: clean compile.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Authoring/PlayerAuthoring.cs
git commit -m "feat: bake Health, Invincible, WeaponState onto player entities"
```

---

## Task 3: EnemyAuthoring.cs + SpawnerAuthoring.cs — bakers for enemies and spawner

**Files:**
- Create: `Assets/Scripts/Authoring/EnemyAuthoring.cs`
- Create: `Assets/Scripts/Authoring/SpawnerAuthoring.cs`

`EnemyAuthoring` is a MonoBehaviour you attach to each enemy prefab. It bakes `EnemyTag`, `EnemyStats`, `Health`, and `Invincible`. `SpawnerAuthoring` is a MonoBehaviour on the spawner scene GameObject. It bakes the three prefab entity references plus initial timer/RNG state into a `SpawnerData` singleton.

- [ ] **Step 1: Create EnemyAuthoring.cs**

```csharp
using Unity.Entities;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Authoring
{
    /// <summary>
    /// Attach to each enemy prefab GameObject.
    /// Baker stamps EnemyTag, EnemyStats, Health, and Invincible onto the entity.
    /// </summary>
    public class EnemyAuthoring : MonoBehaviour
    {
        [Header("Enemy Stats")]
        public int hp;
        public float moveSpeed;
        public int contactDamage;
        public int xpValue;

        class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new EnemyTag());
                AddComponent(entity, new EnemyStats
                {
                    MoveSpeed     = authoring.moveSpeed,
                    ContactDamage = authoring.contactDamage,
                    XpValue       = authoring.xpValue
                });
                AddComponent(entity, new Health
                {
                    Current = authoring.hp,
                    Max     = authoring.hp
                });
            }
        }
    }
}
```

- [ ] **Step 2: Create SpawnerAuthoring.cs**

```csharp
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Authoring
{
    /// <summary>
    /// Attach to the EnemySpawner GameObject in the game scene.
    /// Baker converts the three prefab references into entity refs stored in SpawnerData.
    /// </summary>
    public class SpawnerAuthoring : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject batPrefab;
        public GameObject zombiePrefab;
        public GameObject skeletonPrefab;

        class Baker : Baker<SpawnerAuthoring>
        {
            public override void Bake(SpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new SpawnerData
                {
                    BatPrefab      = GetEntity(authoring.batPrefab,      TransformUsageFlags.Dynamic),
                    ZombiePrefab   = GetEntity(authoring.zombiePrefab,   TransformUsageFlags.Dynamic),
                    SkeletonPrefab = GetEntity(authoring.skeletonPrefab, TransformUsageFlags.Dynamic),
                    Timer          = 3f,
                    Rng            = Random.CreateFromIndex(42)
                });
            }
        }
    }
}
```

- [ ] **Step 3: Check compilation**

In Unity console, verify no errors. Expected: clean compile.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Authoring/EnemyAuthoring.cs Assets/Scripts/Authoring/EnemyAuthoring.cs.meta Assets/Scripts/Authoring/SpawnerAuthoring.cs Assets/Scripts/Authoring/SpawnerAuthoring.cs.meta
git commit -m "feat: add EnemyAuthoring and SpawnerAuthoring bakers"
```

---

## Task 4: EnemyMovementSystem — chase nearest player

**Files:**
- Create: `Assets/Scripts/Systems/EnemyMovementSystem.cs`

Each frame, build a `NativeArray` of player positions. Pass it to a Burst-compiled `IJobEntity` that moves each enemy toward the nearest player. Dispose the array after the job dependency resolves.

- [ ] **Step 1: Create EnemyMovementSystem.cs**

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
    /// Moves each enemy toward the nearest player.
    /// Burst-compiled. O(enemies × players) — fine for current scale.
    /// </summary>
    [BurstCompile]
    public partial struct EnemyMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag, LocalTransform>()
                .Build();

            if (playerQuery.IsEmpty) return;

            var playerPositions = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            var job = new MoveTowardPlayerJob
            {
                PlayerPositions = playerPositions,
                DeltaTime       = SystemAPI.Time.DeltaTime
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
            playerPositions.Dispose(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(EnemyTag))]
        partial struct MoveTowardPlayerJob : IJobEntity
        {
            [ReadOnly] public NativeArray<LocalTransform> PlayerPositions;
            public float DeltaTime;

            void Execute(in EnemyStats stats, ref LocalTransform transform)
            {
                float3 nearest  = PlayerPositions[0].Position;
                float  minDistSq = math.distancesq(transform.Position, nearest);

                for (int i = 1; i < PlayerPositions.Length; i++)
                {
                    float d = math.distancesq(transform.Position, PlayerPositions[i].Position);
                    if (d < minDistSq)
                    {
                        minDistSq = d;
                        nearest   = PlayerPositions[i].Position;
                    }
                }

                float3 dir = math.normalizesafe(nearest - transform.Position);
                transform.Position += dir * stats.MoveSpeed * DeltaTime;
            }
        }
    }
}
```

- [ ] **Step 2: Check compilation, enter play mode**

Enter play mode in Unity. Enemies won't spawn yet (prefabs don't exist). No errors expected. Check console for compilation errors only.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Systems/EnemyMovementSystem.cs Assets/Scripts/Systems/EnemyMovementSystem.cs.meta
git commit -m "feat: add Burst-compiled EnemyMovementSystem (chase nearest player)"
```

---

## Task 5: InvincibilitySystem — tick down iframes

**Files:**
- Create: `Assets/Scripts/Systems/InvincibilitySystem.cs`

Simple Burst system that decrements `Invincible.Timer` by `dt`, clamped to 0. Runs on all entities that have the `Invincible` component (players only at this stage).

- [ ] **Step 1: Create InvincibilitySystem.cs**

```csharp
using Unity.Burst;
using Unity.Entities;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Ticks Invincible.Timer down each frame. When Timer reaches 0, the entity
    /// can take contact damage again.
    /// </summary>
    [BurstCompile]
    public partial struct InvincibilitySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            new TickInvincibleJob { DeltaTime = dt }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct TickInvincibleJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(ref Invincible invincible)
            {
                if (invincible.Timer > 0f)
                    invincible.Timer = Unity.Mathematics.math.max(0f, invincible.Timer - DeltaTime);
            }
        }
    }
}
```

- [ ] **Step 2: Check compilation**

Verify no errors in Unity console.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Systems/InvincibilitySystem.cs Assets/Scripts/Systems/InvincibilitySystem.cs.meta
git commit -m "feat: add InvincibilitySystem (tick contact-damage iframes)"
```

---

## Task 6: ContactDamageSystem — enemies deal damage to nearby players

**Files:**
- Create: `Assets/Scripts/Systems/ContactDamageSystem.cs`

Builds `NativeArray`s of player entities, positions, and uses `ComponentLookup` to write Health and Invincible directly. Runs single-threaded (`.Run()`) to avoid write races — player count is ≤4 so this is never a bottleneck. Uses `[NativeDisableParallelForRestriction]` because safety checker requires it for ComponentLookup writes, even in single-threaded jobs.

- [ ] **Step 1: Create ContactDamageSystem.cs**

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
    /// Each frame, checks each enemy against all players.
    /// If within ContactRadius and the player is not invincible: deal ContactDamage
    /// and set player Invincible.Timer = 1.0s.
    /// Runs single-threaded to avoid write races on shared Health/Invincible components.
    /// </summary>
    [BurstCompile]
    public partial struct ContactDamageSystem : ISystem
    {
        ComponentLookup<Health>     _healthLookup;
        ComponentLookup<Invincible> _invincibleLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _healthLookup     = state.GetComponentLookup<Health>(isReadOnly: false);
            _invincibleLookup = state.GetComponentLookup<Invincible>(isReadOnly: false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _healthLookup.Update(ref state);
            _invincibleLookup.Update(ref state);

            var playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag, LocalTransform, Health, Invincible>()
                .Build();

            if (playerQuery.IsEmpty) return;

            var playerEntities   = playerQuery.ToEntityArray(Allocator.Temp);
            var playerTransforms = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            new ContactDamageJob
            {
                PlayerEntities   = playerEntities,
                PlayerTransforms = playerTransforms,
                HealthLookup     = _healthLookup,
                InvincibleLookup = _invincibleLookup
            }.Run(); // Single-threaded — multiple enemies can target the same player

            playerEntities.Dispose();
            playerTransforms.Dispose();
        }

        [BurstCompile]
        [WithAll(typeof(EnemyTag))]
        partial struct ContactDamageJob : IJobEntity
        {
            const float ContactRadius = 0.5f;

            [ReadOnly] public NativeArray<Entity>         PlayerEntities;
            [ReadOnly] public NativeArray<LocalTransform> PlayerTransforms;

            [NativeDisableParallelForRestriction] public ComponentLookup<Health>     HealthLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<Invincible> InvincibleLookup;

            void Execute(in EnemyStats stats, in LocalTransform transform)
            {
                for (int i = 0; i < PlayerEntities.Length; i++)
                {
                    float dist = math.distance(transform.Position.xy, PlayerTransforms[i].Position.xy);
                    if (dist > ContactRadius) continue;

                    var inv = InvincibleLookup[PlayerEntities[i]];
                    if (inv.Timer > 0f) continue;

                    var hp = HealthLookup[PlayerEntities[i]];
                    hp.Current      -= stats.ContactDamage;
                    inv.Timer        = 1.0f;

                    HealthLookup[PlayerEntities[i]]     = hp;
                    InvincibleLookup[PlayerEntities[i]] = inv;
                }
            }
        }
    }
}
```

- [ ] **Step 2: Check compilation**

Verify no errors in Unity console.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Systems/ContactDamageSystem.cs Assets/Scripts/Systems/ContactDamageSystem.cs.meta
git commit -m "feat: add ContactDamageSystem (enemies deal damage with iframes)"
```

---

## Task 7: WhipSystem — spawn HitArc entities on swing

**Files:**
- Create: `Assets/Scripts/Systems/WhipSystem.cs`

For each player with `WeaponState + MoveInput + LocalTransform`: tick `SwingTimer` down. When it hits 0, create a `HitArc` entity via ECB with the player's current facing direction (last `MoveInput`; default right if zero) and reset the timer.

ECB uses `EndSimulationEntityCommandBufferSystem` so HitArc entities become real at end of frame and are processed by `HitArcSystem` on the next frame. At 60fps this is imperceptible.

- [ ] **Step 1: Create WhipSystem.cs**

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Ticks each player's WeaponState.SwingTimer. When it reaches 0, spawns a
    /// HitArc entity encoding the arc's origin, direction, range, and damage.
    /// HitArcSystem consumes and destroys HitArc entities.
    /// </summary>
    [BurstCompile]
    public partial struct WhipSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (weaponState, moveInput, transform) in
                SystemAPI.Query<RefRW<WeaponState>, RefRO<MoveInput>, RefRO<LocalTransform>>()
                    .WithAll<PlayerTag>())
            {
                ref var ws = ref weaponState.ValueRW;
                ws.SwingTimer -= dt;

                if (ws.SwingTimer > 0f) continue;

                float2 dir = moveInput.ValueRO.Value;
                if (math.lengthsq(dir) < 0.01f)
                    dir = new float2(1f, 0f); // default right when player is idle

                var arcEntity = ecb.CreateEntity();
                ecb.AddComponent(arcEntity, new HitArc
                {
                    Damage     = ws.Damage,
                    Direction  = math.normalize(dir),
                    Range      = ws.Range,
                    ArcDegrees = ws.ArcDegrees,
                    Origin     = transform.ValueRO.Position
                });

                ws.SwingTimer = ws.SwingCooldown;
            }
        }
    }
}
```

- [ ] **Step 2: Check compilation**

Verify no errors in Unity console.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Systems/WhipSystem.cs Assets/Scripts/Systems/WhipSystem.cs.meta
git commit -m "feat: add WhipSystem (spawn HitArc entity on swing timer)"
```

---

## Task 8: HitArcSystem — apply arc damage to enemies, destroy arc

**Files:**
- Create: `Assets/Scripts/Systems/HitArcSystem.cs`

For each `HitArc` entity: build enemy position/entity arrays, check each enemy against arc (range + half-angle), apply `Damage` to `Health.Current` via `ComponentLookup`, destroy the `HitArc` via ECB. Runs single-threaded to avoid races when multiple arcs hit the same enemy.

- [ ] **Step 1: Create HitArcSystem.cs**

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
    /// Processes each HitArc entity created by WhipSystem.
    /// For each arc, checks all enemies within Range whose angle from Direction
    /// is within ArcDegrees/2; subtracts Damage from their Health.Current.
    /// Destroys the HitArc entity after processing.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(WhipSystem))]
    public partial struct HitArcSystem : ISystem
    {
        ComponentLookup<Health> _healthLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _healthLookup = state.GetComponentLookup<Health>(isReadOnly: false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _healthLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            var enemyQuery      = SystemAPI.QueryBuilder().WithAll<EnemyTag, LocalTransform, Health>().Build();
            var enemyEntities   = enemyQuery.ToEntityArray(Allocator.TempJob);
            var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            new ProcessHitArcJob
            {
                EnemyEntities   = enemyEntities,
                EnemyTransforms = enemyTransforms,
                HealthLookup    = _healthLookup,
                Ecb             = ecb.AsParallelWriter()
            }.Run(); // Single-threaded — multiple arcs could hit the same enemy

            enemyEntities.Dispose();
            enemyTransforms.Dispose();
        }

        [BurstCompile]
        partial struct ProcessHitArcJob : IJobEntity
        {
            [ReadOnly] public NativeArray<Entity>         EnemyEntities;
            [ReadOnly] public NativeArray<LocalTransform> EnemyTransforms;

            [NativeDisableParallelForRestriction] public ComponentLookup<Health> HealthLookup;

            public EntityCommandBuffer.ParallelWriter Ecb;

            void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, in HitArc arc)
            {
                float halfArcRad = math.radians(arc.ArcDegrees * 0.5f);
                float2 dir       = math.normalizesafe(arc.Direction);

                for (int i = 0; i < EnemyEntities.Length; i++)
                {
                    float2 toEnemy = EnemyTransforms[i].Position.xy - arc.Origin.xy;
                    float  dist    = math.length(toEnemy);

                    if (dist > arc.Range) continue;

                    // Angle check — acos(dot) <= half-arc
                    float2 toEnemyNorm = math.normalizesafe(toEnemy);
                    float  dot         = math.dot(dir, toEnemyNorm);
                    float  angle       = math.acos(math.clamp(dot, -1f, 1f));

                    if (angle > halfArcRad) continue;

                    var hp = HealthLookup[EnemyEntities[i]];
                    hp.Current -= (int)arc.Damage;
                    HealthLookup[EnemyEntities[i]] = hp;
                }

                Ecb.DestroyEntity(chunkIndex, entity);
            }
        }
    }
}
```

- [ ] **Step 2: Check compilation**

Verify no errors in Unity console.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Systems/HitArcSystem.cs Assets/Scripts/Systems/HitArcSystem.cs.meta
git commit -m "feat: add HitArcSystem (arc damage to enemies, destroy HitArc)"
```

---

## Task 9: HealthSystem — destroy entities at 0 HP

**Files:**
- Create: `Assets/Scripts/Systems/HealthSystem.cs`

Runs after all damage systems. Iterates all `Health` entities; if `Current <= 0`, destroys the entity via ECB. For player entities, logs to console (death screen is future work). Not Burst-compiled because it calls `Debug.Log` (managed API).

- [ ] **Step 1: Create HealthSystem.cs**

```csharp
using Unity.Entities;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Runs after all damage systems. Destroys any entity whose Health.Current
    /// has dropped to or below 0. Logs a message for player deaths.
    /// Not Burst-compiled — calls Debug.Log.
    /// </summary>
    [UpdateAfter(typeof(ContactDamageSystem))]
    [UpdateAfter(typeof(HitArcSystem))]
    public partial struct HealthSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (health, entity) in
                SystemAPI.Query<RefRO<Health>>().WithEntityAccess())
            {
                if (health.ValueRO.Current > 0) continue;

                if (SystemAPI.HasComponent<PlayerTag>(entity))
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

- [ ] **Step 2: Check compilation**

Verify no errors in Unity console.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Systems/HealthSystem.cs Assets/Scripts/Systems/HealthSystem.cs.meta
git commit -m "feat: add HealthSystem (destroy entities at 0 HP, log player death)"
```

---

## Task 10: EnemySpawnerSystem — timed wave spawning

**Files:**
- Create: `Assets/Scripts/Systems/EnemySpawnerSystem.cs`

`SystemBase` (not `ISystem`) because it calls `EntityManager.Instantiate` on the main thread. Every 3 seconds, computes player centroid, picks 5–8 enemies with weighted random (Bat 60%, Zombie 25%, Skeleton 15%), and places them at random points 12 units from the centroid.

- [ ] **Step 1: Create EnemySpawnerSystem.cs**

```csharp
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Non-Burst SystemBase. Every 3 seconds spawns a burst of 5-8 enemies
    /// around the player centroid at radius 12 units.
    /// Weighted random: Bat 60%, Zombie 25%, Skeleton 15%.
    /// </summary>
    public partial class EnemySpawnerSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingletonEntity<SpawnerData>(out var spawnerEntity))
                return;

            var spawner = EntityManager.GetComponentData<SpawnerData>(spawnerEntity);
            spawner.Timer -= SystemAPI.Time.DeltaTime;

            if (spawner.Timer > 0f)
            {
                EntityManager.SetComponentData(spawnerEntity, spawner);
                return;
            }

            // Compute player centroid
            var playerQuery = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>()
            );

            if (playerQuery.IsEmpty)
            {
                playerQuery.Dispose();
                spawner.Timer = 3f;
                EntityManager.SetComponentData(spawnerEntity, spawner);
                return;
            }

            var playerTransforms = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            playerQuery.Dispose();

            float3 centroid = float3.zero;
            for (int i = 0; i < playerTransforms.Length; i++)
                centroid += playerTransforms[i].Position;
            centroid /= playerTransforms.Length;
            playerTransforms.Dispose();

            // Spawn burst
            int count = spawner.Rng.NextInt(5, 9); // 5 inclusive, 9 exclusive → 5-8

            for (int i = 0; i < count; i++)
            {
                float angle    = spawner.Rng.NextFloat(0f, math.PI * 2f);
                float3 spawnPos = new float3(
                    centroid.x + math.cos(angle) * 12f,
                    centroid.y + math.sin(angle) * 12f,
                    0f
                );

                float  roll   = spawner.Rng.NextFloat();
                Entity prefab = roll < 0.60f ? spawner.BatPrefab :
                                roll < 0.85f ? spawner.ZombiePrefab :
                                               spawner.SkeletonPrefab;

                var e = EntityManager.Instantiate(prefab);
                EntityManager.SetComponentData(e, LocalTransform.FromPosition(spawnPos));
            }

            spawner.Timer = 3f;
            EntityManager.SetComponentData(spawnerEntity, spawner);
        }
    }
}
```

- [ ] **Step 2: Check compilation**

Verify no errors in Unity console.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Systems/EnemySpawnerSystem.cs Assets/Scripts/Systems/EnemySpawnerSystem.cs.meta
git commit -m "feat: add EnemySpawnerSystem (timed wave spawning around player centroid)"
```

---

## Task 11: Enemy prefabs + EnemySpawner scene object (Unity MCP)

**Files:**
- Unity: `Assets/Prefabs/Enemies/Bat.prefab`, `Zombie.prefab`, `Skeleton.prefab`
- Unity: `Assets/Scenes/4_SampleScene.unity` — add EnemySpawner GameObject

This task uses Unity MCP tools to create the enemy prefabs and wire up the spawner in the scene. Stat values come from the spec: Bat (hp=10, speed=2.5, contactDamage=10, xpValue=1), Zombie (hp=40, speed=1.0, contactDamage=20, xpValue=5), Skeleton (hp=75, speed=1.8, contactDamage=25, xpValue=10).

### Step-by-step using Unity MCP

- [ ] **Step 1: Create materials for each enemy**

Use `manage_material` to create three materials in `Assets/Materials/Enemies/`:
- `BatMaterial.mat` — red (`{r:1,g:0,b:0,a:1}`)
- `ZombieMaterial.mat` — green (`{r:0,g:1,b:0,a:1}`)
- `SkeletonMaterial.mat` — blue (`{r:0,g:0,b:1,a:1}`)

Use a URP/2D Lit or Sprites/Default shader. Verify materials appear in Project window.

- [ ] **Step 2: Create Bat prefab**

1. Create a Quad GameObject named "Bat" with scale (0.8, 0.8, 1)
2. Assign `BatMaterial` to its MeshRenderer
3. Add `EnemyAuthoring` component with: hp=10, moveSpeed=2.5, contactDamage=10, xpValue=1
4. Save as prefab: `Assets/Prefabs/Enemies/Bat.prefab`

- [ ] **Step 3: Create Zombie prefab**

1. Create a Quad GameObject named "Zombie" with scale (1, 1, 1)
2. Assign `ZombieMaterial`
3. Add `EnemyAuthoring`: hp=40, moveSpeed=1.0, contactDamage=20, xpValue=5
4. Save as prefab: `Assets/Prefabs/Enemies/Zombie.prefab`

- [ ] **Step 4: Create Skeleton prefab**

1. Create a Quad GameObject named "Skeleton" with scale (1, 1, 1)
2. Assign `SkeletonMaterial`
3. Add `EnemyAuthoring`: hp=75, moveSpeed=1.8, contactDamage=25, xpValue=10
4. Save as prefab: `Assets/Prefabs/Enemies/Skeleton.prefab`

- [ ] **Step 5: Add EnemySpawner to 4_SampleScene**

1. Load `4_SampleScene` (build index 3)
2. Create an empty GameObject named "EnemySpawner" at position (0, 0, 0)
3. Add `SpawnerAuthoring` component to it
4. Assign `batPrefab`, `zombiePrefab`, `skeletonPrefab` fields to the three prefabs just created
5. Save the scene

- [ ] **Step 6: Enter play mode and verify**

Enter play mode. Expected behaviour:
- After ~3 seconds, a burst of 5–8 colored quads appears around the player(s)
- Quads move toward players
- Touching a quad reduces player HP — open **Window > Entities > Hierarchy**, select the player entity, and confirm `Health.Current` decreases in the Inspector panel on each contact hit (once per 1-second cooldown)
- Console logs `[HealthSystem] Player 0 died.` when HP hits 0

Check Unity console — no errors expected.

- [ ] **Step 7: Commit**

```bash
git add Assets/Prefabs/ Assets/Materials/Enemies/ Assets/Scenes/4_SampleScene.unity
git commit -m "feat: add enemy prefabs (Bat/Zombie/Skeleton) and EnemySpawner to game scene"
```

---

## Integration Verification

After all tasks complete, enter play mode in `4_SampleScene` and confirm:

1. **Enemies spawn** — colored quads appear outside camera view every ~3 seconds
2. **Enemies chase** — quads move toward player position
3. **Contact damage** — walking into quads reduces player HP (one damage instance per 1-second cooldown)
4. **Whip fires** — every 0.5s a HitArc entity is created (visible in DOTS Runtime Inspector)
5. **Whip kills** — enemies disappear when hit by arc (bat dies in 1 hit at 10 damage)
6. **Player death** — console prints `[HealthSystem] Player 0 died.` when HP reaches 0; player entity removed
7. **No errors** — Unity console clean throughout

