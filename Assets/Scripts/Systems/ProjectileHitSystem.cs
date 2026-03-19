using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Checks each Projectile against all living enemies.
    /// On first overlap within HitRadius: subtracts Damage from the enemy's Health,
    /// then destroys the projectile (each bolt hits exactly one enemy).
    /// Runs single-threaded to avoid write races on shared Health components.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(ProjectileMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ProjectileHitSystem : ISystem
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

            new HitCheckJob
            {
                EnemyEntities   = enemyEntities,
                EnemyTransforms = enemyTransforms,
                HealthLookup    = _healthLookup,
                Ecb             = ecb
            }.Run();

            enemyEntities.Dispose();
            enemyTransforms.Dispose();
        }

        [BurstCompile]
        partial struct HitCheckJob : IJobEntity
        {
            const float HitRadius = 0.4f;

            [ReadOnly] public NativeArray<Entity>         EnemyEntities;
            [ReadOnly] public NativeArray<LocalTransform> EnemyTransforms;

            [NativeDisableParallelForRestriction] public ComponentLookup<Health> HealthLookup;

            public EntityCommandBuffer Ecb;

            void Execute(Entity entity, in Projectile proj, in LocalTransform transform)
            {
                for (int i = 0; i < EnemyEntities.Length; i++)
                {
                    float dist = math.distance(transform.Position.xy, EnemyTransforms[i].Position.xy);
                    if (dist > HitRadius) continue;

                    var hp = HealthLookup[EnemyEntities[i]];
                    hp.Current -= (int)proj.Damage;
                    HealthLookup[EnemyEntities[i]] = hp;

                    var dmgEvt = Ecb.CreateEntity();
                    Ecb.AddComponent(dmgEvt, new DamageNumberEvent
                    {
                        WorldPosition = EnemyTransforms[i].Position,
                        Damage        = (int)proj.Damage
                    });

                    Ecb.DestroyEntity(entity); // bolt hits once then disappears
                    return;
                }
            }
        }
    }
}
