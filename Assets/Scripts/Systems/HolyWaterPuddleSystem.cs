using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Ticks all HolyWaterPuddle entities:
    ///   - Counts down Lifetime; despawns when it reaches 0.
    ///   - Every TickCooldown seconds damages ALL enemies within Radius.
    ///
    /// Runs single-threaded (.Run()) because multiple puddles can write to the
    /// same enemy Health simultaneously.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(HolyWaterSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct HolyWaterPuddleSystem : ISystem
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

            var enemyQuery = SystemAPI.QueryBuilder()
                .WithAll<EnemyTag, LocalTransform, Health>().Build();

            var enemyEntities   = enemyQuery.ToEntityArray(Allocator.TempJob);
            var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            new PuddleTickJob
            {
                EnemyEntities   = enemyEntities,
                EnemyTransforms = enemyTransforms,
                HealthLookup    = _healthLookup,
                DeltaTime       = dt,
                Ecb             = ecb
            }.Run();

            enemyEntities.Dispose();
            enemyTransforms.Dispose();
        }

        [BurstCompile]
        partial struct PuddleTickJob : IJobEntity
        {
            [ReadOnly] public NativeArray<Entity>         EnemyEntities;
            [ReadOnly] public NativeArray<LocalTransform> EnemyTransforms;
            [NativeDisableParallelForRestriction] public ComponentLookup<Health> HealthLookup;
            public EntityCommandBuffer Ecb;
            public float DeltaTime;

            void Execute(ref HolyWaterPuddle puddle, in LocalTransform transform, Entity entity)
            {
                puddle.Lifetime -= DeltaTime;
                if (puddle.Lifetime <= 0f)
                {
                    Ecb.DestroyEntity(entity);
                    return;
                }

                puddle.TickTimer -= DeltaTime;
                if (puddle.TickTimer > 0f) return;

                puddle.TickTimer = puddle.TickCooldown;

                int damage = (int)puddle.Damage;

                for (int i = 0; i < EnemyEntities.Length; i++)
                {
                    float dist = math.distance(
                        transform.Position.xy,
                        EnemyTransforms[i].Position.xy);

                    if (dist > puddle.Radius) continue;

                    var hp = HealthLookup[EnemyEntities[i]];
                    hp.Current -= damage;
                    HealthLookup[EnemyEntities[i]] = hp;

                    var dmgEvt = Ecb.CreateEntity();
                    Ecb.AddComponent(dmgEvt, new DamageNumberEvent
                    {
                        WorldPosition = EnemyTransforms[i].Position,
                        Damage        = damage
                    });

                    // Knockback: push enemy away from puddle center
                    float2 pushDir = math.normalizesafe(
                        EnemyTransforms[i].Position.xy - transform.Position.xy);
                    Ecb.SetComponent(EnemyEntities[i], new Knockback { Velocity = pushDir * 3f });
                }
            }
        }
    }
}
