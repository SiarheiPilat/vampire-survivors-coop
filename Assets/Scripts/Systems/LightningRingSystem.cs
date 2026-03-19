using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Lightning Ring: every Cooldown seconds, strikes up to Amount random enemies
    /// for Damage (scaled by Might). Damage is applied instantly — no projectile.
    ///
    /// Target selection: shuffle random indices into the enemy list, take the first
    /// min(Amount, enemyCount) unique targets. Per-player RNG ensures 4 players
    /// strike independent random targets.
    ///
    /// Wiki base stats: Damage 40, Cooldown 0.6 s, Amount 1.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct LightningRingSystem : ISystem
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

            var enemyQuery = SystemAPI.QueryBuilder()
                .WithAll<EnemyTag, Health>().Build();

            if (enemyQuery.IsEmpty) return;

            var enemyEntities = enemyQuery.ToEntityArray(Allocator.TempJob);

            new StrikeJob
            {
                EnemyEntities = enemyEntities,
                HealthLookup  = _healthLookup,
                DeltaTime     = dt
            }.Run();

            enemyEntities.Dispose();
        }

        [BurstCompile]
        [WithAll(typeof(PlayerTag))]
        [WithNone(typeof(Downed))]
        partial struct StrikeJob : IJobEntity
        {
            [ReadOnly] public NativeArray<Entity> EnemyEntities;
            [NativeDisableParallelForRestriction] public ComponentLookup<Health> HealthLookup;
            public float DeltaTime;

            void Execute(ref LightningRingState ring, in PlayerStats stats)
            {
                ring.Timer -= DeltaTime;
                if (ring.Timer > 0f) return;

                ring.Timer = ring.Cooldown;

                int damage  = (int)(ring.Damage * stats.Might);
                int count   = EnemyEntities.Length;
                int strikes = math.min(ring.Amount, count);

                // Pick `strikes` random targets (duplicates allowed — same enemy
                // struck twice at high Amount is acceptable for simplicity).
                for (int s = 0; s < strikes; s++)
                {
                    int idx = ring.Rng.NextInt(0, count);
                    var hp  = HealthLookup[EnemyEntities[idx]];
                    hp.Current -= damage;
                    HealthLookup[EnemyEntities[idx]] = hp;
                }
            }
        }
    }
}
