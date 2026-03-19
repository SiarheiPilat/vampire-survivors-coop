using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Garlic emits a damaging pulse every Cooldown seconds, hitting all enemies
    /// within Range of the player simultaneously.
    /// Wiki base stats: Damage 10, Range 1.5 u, Cooldown 1.5 s.
    /// Runs single-threaded to avoid write races on shared Health components.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct GarlicSystem : ISystem
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

            var enemyQuery      = SystemAPI.QueryBuilder().WithAll<EnemyTag, LocalTransform, Health>().Build();
            if (enemyQuery.IsEmpty) return;

            var enemyEntities   = enemyQuery.ToEntityArray(Allocator.TempJob);
            var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            new GarlicPulseJob
            {
                EnemyEntities   = enemyEntities,
                EnemyTransforms = enemyTransforms,
                HealthLookup    = _healthLookup,
                DeltaTime       = dt
            }.Run();

            enemyEntities.Dispose();
            enemyTransforms.Dispose();
        }

        [BurstCompile]
        [WithAll(typeof(PlayerTag))]
        [WithNone(typeof(Downed))]
        partial struct GarlicPulseJob : IJobEntity
        {
            [ReadOnly] public NativeArray<Entity>         EnemyEntities;
            [ReadOnly] public NativeArray<LocalTransform> EnemyTransforms;

            [NativeDisableParallelForRestriction] public ComponentLookup<Health> HealthLookup;

            public float DeltaTime;

            void Execute(ref GarlicState garlic, in LocalTransform transform, in PlayerStats stats)
            {
                garlic.Timer -= DeltaTime;
                if (garlic.Timer > 0f) return;

                garlic.Timer = garlic.Cooldown * stats.CooldownMult;

                int damage = (int)(garlic.Damage * stats.Might);

                for (int i = 0; i < EnemyEntities.Length; i++)
                {
                    float dist = math.distance(transform.Position.xy, EnemyTransforms[i].Position.xy);
                    if (dist > garlic.Range) continue;

                    var hp = HealthLookup[EnemyEntities[i]];
                    hp.Current -= damage;
                    HealthLookup[EnemyEntities[i]] = hp;
                }
            }
        }
    }
}
