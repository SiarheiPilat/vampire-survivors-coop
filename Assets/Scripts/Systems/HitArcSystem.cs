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
