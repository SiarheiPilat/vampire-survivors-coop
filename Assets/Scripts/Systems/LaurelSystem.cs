using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Laurel — defensive shield that grants periodic invincibility.
    /// Every Cooldown × CooldownMult seconds, sets the player's Invincible.Timer to
    /// InvulDuration × DurationMult. ContactDamageSystem skips damage while Invincible.Timer > 0.
    ///
    /// Evolved (Crimson Shroud = Laurel + Metaglio Left + Metaglio Right):
    ///   Same periodic invulnerability, plus fires a RetaliationDamage AoE explosion in
    ///   RetaliationRadius around the player each pulse. Cooldown reduced to 8.0 s.
    ///   Damage cap (MaxDamageCap=10) is applied by ContactDamageSystem via ComponentLookup.
    ///
    /// Wiki base stats: Cooldown 10.0 s, InvulDuration ~0.5 s.
    /// Burst-compiled; IJobEntity runs single-threaded to avoid Health write races.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct LaurelSystem : ISystem
    {
        ComponentLookup<Invincible> _invincibleLookup;
        ComponentLookup<Health>     _healthLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _invincibleLookup = state.GetComponentLookup<Invincible>(isReadOnly: false);
            _healthLookup     = state.GetComponentLookup<Health>(isReadOnly: false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _invincibleLookup.Update(ref state);
            _healthLookup.Update(ref state);

            float dt = SystemAPI.Time.DeltaTime;

            var enemyQuery = SystemAPI.QueryBuilder()
                .WithAll<EnemyTag, LocalTransform, Health>().Build();
            var enemyEntities   = enemyQuery.ToEntityArray(Allocator.TempJob);
            var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            new LaurelPulseJob
            {
                EnemyEntities    = enemyEntities,
                EnemyTransforms  = enemyTransforms,
                InvincibleLookup = _invincibleLookup,
                HealthLookup     = _healthLookup,
                DeltaTime        = dt,
                Ecb              = ecb,
            }.Run();

            enemyEntities.Dispose();
            enemyTransforms.Dispose();
        }

        [BurstCompile]
        [WithAll(typeof(PlayerTag))]
        [WithNone(typeof(Downed))]
        partial struct LaurelPulseJob : IJobEntity
        {
            [ReadOnly] public NativeArray<Entity>         EnemyEntities;
            [ReadOnly] public NativeArray<LocalTransform> EnemyTransforms;

            [NativeDisableParallelForRestriction] public ComponentLookup<Invincible> InvincibleLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<Health>     HealthLookup;

            public float               DeltaTime;
            public EntityCommandBuffer Ecb;

            void Execute(Entity entity, ref LaurelState laurel, in LocalTransform transform, in PlayerStats stats)
            {
                laurel.Timer -= DeltaTime;
                if (laurel.Timer > 0f) return;

                laurel.Timer = laurel.Cooldown * stats.CooldownMult;

                // Grant invulnerability
                float invulDuration = laurel.InvulDuration * stats.DurationMult;
                if (InvincibleLookup.HasComponent(entity))
                {
                    var inv = InvincibleLookup[entity];
                    inv.Timer = math.max(inv.Timer, invulDuration);
                    InvincibleLookup[entity] = inv;
                }

                // Crimson Shroud retaliation pulse — AoE explosion around the player
                if (!laurel.IsEvolved || laurel.RetaliationDamage <= 0f) return;

                int    damage   = (int)(laurel.RetaliationDamage * stats.Might);
                float  radius   = laurel.RetaliationRadius * stats.AreaMult;
                float2 playerPos = transform.Position.xy;

                for (int i = 0; i < EnemyEntities.Length; i++)
                {
                    if (math.distance(playerPos, EnemyTransforms[i].Position.xy) > radius) continue;

                    var hp = HealthLookup[EnemyEntities[i]];
                    hp.Current -= damage;
                    HealthLookup[EnemyEntities[i]] = hp;

                    var dmgEvt = Ecb.CreateEntity();
                    Ecb.AddComponent(dmgEvt, new DamageNumberEvent
                    {
                        WorldPosition = EnemyTransforms[i].Position,
                        Damage        = damage
                    });
                }
            }
        }
    }
}
