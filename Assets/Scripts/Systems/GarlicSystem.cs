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

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            new GarlicPulseJob
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
        [WithAll(typeof(PlayerTag))]
        [WithNone(typeof(Downed))]
        partial struct GarlicPulseJob : IJobEntity
        {
            [ReadOnly] public NativeArray<Entity>         EnemyEntities;
            [ReadOnly] public NativeArray<LocalTransform> EnemyTransforms;

            [NativeDisableParallelForRestriction] public ComponentLookup<Health> HealthLookup;

            public float               DeltaTime;
            public EntityCommandBuffer Ecb;

            void Execute(Entity entity, ref GarlicState garlic, in LocalTransform transform, in PlayerStats stats)
            {
                garlic.Timer -= DeltaTime;
                if (garlic.Timer > 0f) return;

                garlic.Timer = garlic.Cooldown * stats.CooldownMult;

                // Soul Eater: heal owning player once per pulse
                if (garlic.IsEvolved && garlic.HealPerPulse > 0f && HealthLookup.HasComponent(entity))
                {
                    var playerHp = HealthLookup[entity];
                    playerHp.Current = math.min(playerHp.Max, playerHp.Current + (int)garlic.HealPerPulse);
                    HealthLookup[entity] = playerHp;
                }

                int damage = (int)(garlic.Damage * stats.Might);

                for (int i = 0; i < EnemyEntities.Length; i++)
                {
                    float dist = math.distance(transform.Position.xy, EnemyTransforms[i].Position.xy);
                    if (dist > garlic.Range) continue;

                    var hp = HealthLookup[EnemyEntities[i]];
                    hp.Current -= damage;
                    HealthLookup[EnemyEntities[i]] = hp;

                    var dmgEvt = Ecb.CreateEntity();
                    Ecb.AddComponent(dmgEvt, new DamageNumberEvent
                    {
                        WorldPosition = EnemyTransforms[i].Position,
                        Damage        = damage
                    });

                    // Knockback: push enemy away from player center
                    float2 pushDir = math.normalizesafe(
                        EnemyTransforms[i].Position.xy - transform.Position.xy);
                    Ecb.SetComponent(EnemyEntities[i], new Knockback { Velocity = pushDir * 4f });
                }
            }
        }
    }
}
