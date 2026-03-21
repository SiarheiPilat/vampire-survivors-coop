using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Ticks all GattiAmariCat entities each frame:
    ///   - Counts down Lifetime; despawns when it reaches 0.
    ///   - Wanders randomly: changes direction every WanderChangeInterval seconds.
    ///   - Attacks ALL enemies within Radius every AttackCooldown seconds.
    ///
    /// Runs single-threaded (.Run()) because multiple cats can write to the
    /// same enemy Health simultaneously.
    /// Wiki: cats wander at ~1 u/s and attack nearby enemies (AoE ~0.5u radius).
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(GattiAmariSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct GattiAmariCatSystem : ISystem
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

            float dt = SystemAPI.Time.DeltaTime;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            var enemyQuery      = SystemAPI.QueryBuilder().WithAll<EnemyTag, LocalTransform, Health>().Build();
            var enemyEntities   = enemyQuery.ToEntityArray(Allocator.TempJob);
            var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            new CatTickJob
            {
                EnemyEntities   = enemyEntities,
                EnemyTransforms = enemyTransforms,
                HealthLookup    = _healthLookup,
                DeltaTime       = dt,
                Ecb             = ecb,
            }.Run();

            enemyEntities.Dispose();
            enemyTransforms.Dispose();
        }

        [BurstCompile]
        partial struct CatTickJob : IJobEntity
        {
            [ReadOnly] public NativeArray<Entity>         EnemyEntities;
            [ReadOnly] public NativeArray<LocalTransform> EnemyTransforms;
            [NativeDisableParallelForRestriction] public ComponentLookup<Health> HealthLookup;
            public EntityCommandBuffer Ecb;
            public float DeltaTime;

            const float WanderSpeed          = 1.5f;  // units per second
            const float WanderChangeInterval = 0.75f; // seconds between direction changes

            void Execute(ref GattiAmariCat cat, ref LocalTransform transform, Entity entity)
            {
                // Lifetime countdown
                cat.Lifetime -= DeltaTime;
                if (cat.Lifetime <= 0f)
                {
                    Ecb.DestroyEntity(entity);
                    return;
                }

                // Wander: change direction periodically
                cat.WanderTimer -= DeltaTime;
                if (cat.WanderTimer <= 0f)
                {
                    float angle   = cat.Rng.NextFloat(0f, math.PI * 2f);
                    cat.WanderDir  = new float2(math.cos(angle), math.sin(angle));
                    cat.WanderTimer = WanderChangeInterval + cat.Rng.NextFloat(0f, 0.5f);
                }

                transform.Position += new float3(cat.WanderDir.x, cat.WanderDir.y, 0f) * WanderSpeed * DeltaTime;

                // Attack nearby enemies
                cat.AttackTimer -= DeltaTime;
                if (cat.AttackTimer > 0f) return;

                float radiusSq = cat.Radius * cat.Radius;
                bool  hitAny   = false;

                for (int i = 0; i < EnemyEntities.Length; i++)
                {
                    float distSq = math.distancesq(
                        transform.Position.xy,
                        EnemyTransforms[i].Position.xy);

                    if (distSq > radiusSq) continue;

                    var hp = HealthLookup[EnemyEntities[i]];
                    hp.Current -= (int)cat.Damage;
                    HealthLookup[EnemyEntities[i]] = hp;

                    var dmgEvt = Ecb.CreateEntity();
                    Ecb.AddComponent(dmgEvt, new DamageNumberEvent
                    {
                        WorldPosition = EnemyTransforms[i].Position,
                        Damage        = (int)cat.Damage
                    });

                    // Knockback: push enemy away from cat
                    float2 pushDir = math.normalizesafe(
                        EnemyTransforms[i].Position.xy - transform.Position.xy);
                    Ecb.SetComponent(EnemyEntities[i], new Knockback { Velocity = pushDir * 4f });

                    hitAny = true;
                }

                cat.AttackTimer = hitAny ? cat.AttackCooldown : 0.1f; // poll faster when no enemies nearby
            }
        }
    }
}
