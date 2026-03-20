using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Manages King Bible: a set of bibles that permanently orbit the player,
    /// dealing damage on contact with enemies.
    ///
    /// Two-phase update each frame:
    ///   1. Spawn phase  — creates KingBibleOrbit entities for any player whose
    ///      KingBibleState.Spawned == false (reuses BulletPrefab for visuals).
    ///   2. Orbit phase  — Burst job moves each bible around its owner and
    ///      applies damage to the first enemy within HitRadius every HitCooldown s.
    ///
    /// Wiki base stats: Damage 10, Radius 1.4 u, AngularSpeed 120°/s, HitCooldown 0.5 s.
    /// </summary>
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct KingBibleSystem : ISystem
    {
        ComponentLookup<LocalTransform> _transformLookup;
        ComponentLookup<Health>         _healthLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
            _healthLookup    = state.GetComponentLookup<Health>(isReadOnly: false);
        }

        // Not BurstCompile — spawn phase uses ECB singleton and managed SystemAPI calls
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _healthLookup.Update(ref state);

            float dt = SystemAPI.Time.DeltaTime;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // ── Phase 1: Spawn bibles for players whose bibles haven't been created yet ──
            if (SystemAPI.HasSingleton<BulletPrefabData>())
            {
                var prefab = SystemAPI.GetSingleton<BulletPrefabData>().BulletPrefab;

                // Pre-collect existing orbit entities so we can destroy stale ones on re-spawn
                var orbitQuery     = SystemAPI.QueryBuilder().WithAll<KingBibleOrbit>().Build();
                var orbitEntities  = orbitQuery.ToEntityArray(Allocator.Temp);
                var orbitData      = orbitQuery.ToComponentDataArray<KingBibleOrbit>(Allocator.Temp);

                foreach (var (bibleState, playerStats, entity) in
                    SystemAPI.Query<RefRW<KingBibleState>, RefRO<PlayerStats>>()
                        .WithAll<PlayerTag>()
                        .WithNone<Downed>()
                        .WithEntityAccess())
                {
                    if (bibleState.ValueRO.Spawned) continue;

                    // Destroy any existing orbit entities for this player (handles re-spawn on evolution)
                    for (int o = 0; o < orbitData.Length; o++)
                        if (orbitData[o].Owner == entity)
                            ecb.DestroyEntity(orbitEntities[o]);

                    bibleState.ValueRW.Spawned = true;

                    int   n         = bibleState.ValueRO.Count;
                    float angleStep = 2f * math.PI / n;
                    float radius    = bibleState.ValueRO.Radius * playerStats.ValueRO.AreaMult;

                    for (int i = 0; i < n; i++)
                    {
                        var bible = ecb.Instantiate(prefab);
                        ecb.AddComponent(bible, new KingBibleOrbit
                        {
                            Owner        = entity,
                            Angle        = i * angleStep,
                            Radius       = radius,
                            AngularSpeed = bibleState.ValueRO.AngularSpeed,
                            Damage       = bibleState.ValueRO.Damage,
                            HitTimer     = 0f,
                            HitCooldown  = bibleState.ValueRO.HitCooldown
                        });
                        // Slightly larger than a bullet so it reads as an orbiting object
                        ecb.SetComponent(bible, LocalTransform.FromPositionRotationScale(
                            float3.zero, quaternion.identity, 0.35f));
                    }
                }

                orbitEntities.Dispose();
                orbitData.Dispose();
            }

            // ── Phase 2: Orbit update + hit detection ──
            var enemyQuery = SystemAPI.QueryBuilder()
                .WithAll<EnemyTag, LocalTransform, Health>().Build();

            // Always update orbit positions even if no enemies
            var enemyEntities   = enemyQuery.ToEntityArray(Allocator.TempJob);
            var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            new OrbitUpdateJob
            {
                TransformLookup = _transformLookup,
                HealthLookup    = _healthLookup,
                EnemyEntities   = enemyEntities,
                EnemyTransforms = enemyTransforms,
                DeltaTime       = dt,
                Ecb             = ecb
            }.Run();

            enemyEntities.Dispose();
            enemyTransforms.Dispose();
        }

        [BurstCompile]
        partial struct OrbitUpdateJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<Health> HealthLookup;

            [ReadOnly] public NativeArray<Entity>         EnemyEntities;
            [ReadOnly] public NativeArray<LocalTransform> EnemyTransforms;

            public float               DeltaTime;
            public EntityCommandBuffer Ecb;

            void Execute(ref KingBibleOrbit orbit, ref LocalTransform transform)
            {
                if (!TransformLookup.HasComponent(orbit.Owner)) return;

                float3 ownerPos = TransformLookup[orbit.Owner].Position;

                // Advance angle, wrap to [0, 2π)
                orbit.Angle = math.fmod(
                    orbit.Angle + orbit.AngularSpeed * DeltaTime,
                    2f * math.PI);

                float2 offset = new float2(
                    math.cos(orbit.Angle),
                    math.sin(orbit.Angle)) * orbit.Radius;

                transform.Position = ownerPos + new float3(offset.x, offset.y, 0f);

                // Hit detection — fires once per HitCooldown window
                orbit.HitTimer -= DeltaTime;
                if (orbit.HitTimer > 0f) return;

                int   damage    = (int)orbit.Damage;
                const float hitRadius = 0.5f;

                for (int i = 0; i < EnemyEntities.Length; i++)
                {
                    float dist = math.distance(
                        transform.Position.xy,
                        EnemyTransforms[i].Position.xy);

                    if (dist > hitRadius) continue;

                    if (HealthLookup.HasComponent(EnemyEntities[i]))
                    {
                        var hp = HealthLookup[EnemyEntities[i]];
                        hp.Current -= damage;
                        HealthLookup[EnemyEntities[i]] = hp;

                        var dmgEvt = Ecb.CreateEntity();
                        Ecb.AddComponent(dmgEvt, new DamageNumberEvent
                        {
                            WorldPosition = EnemyTransforms[i].Position,
                            Damage        = damage
                        });

                        // Knockback: push enemy away from bible position
                        float2 pushDir = math.normalizesafe(
                            EnemyTransforms[i].Position.xy - transform.Position.xy);
                        Ecb.SetComponent(EnemyEntities[i], new Knockback { Velocity = pushDir * 5f });
                    }

                    orbit.HitTimer = orbit.HitCooldown;
                    break; // one hit per cooldown window
                }
            }
        }
    }
}
