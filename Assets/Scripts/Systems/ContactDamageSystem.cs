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
            // InvincibilitySystem schedules a parallel job on Invincible; complete it
            // before we read/write Invincible in ContactDamageJob.
            state.Dependency.Complete();

            _healthLookup.Update(ref state);
            _invincibleLookup.Update(ref state);

            var playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag, LocalTransform, Health, Invincible, PlayerStats>()
                .WithNone<Downed>()
                .Build();

            if (playerQuery.IsEmpty) return;

            var playerEntities   = playerQuery.ToEntityArray(Allocator.TempJob);
            var playerTransforms = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            var playerStats      = playerQuery.ToComponentDataArray<PlayerStats>(Allocator.TempJob);

            new ContactDamageJob
            {
                PlayerEntities   = playerEntities,
                PlayerTransforms = playerTransforms,
                PlayerStats      = playerStats,
                HealthLookup     = _healthLookup,
                InvincibleLookup = _invincibleLookup
            }.Run(); // Single-threaded — multiple enemies can target the same player

            playerEntities.Dispose();
            playerTransforms.Dispose();
            playerStats.Dispose();
        }

        [BurstCompile]
        [WithAll(typeof(EnemyTag))]
        partial struct ContactDamageJob : IJobEntity
        {
            const float ContactRadius = 0.5f;
            const float InvincibilityDuration = 1.0f;

            [ReadOnly] public NativeArray<Entity>         PlayerEntities;
            [ReadOnly] public NativeArray<LocalTransform> PlayerTransforms;
            [ReadOnly] public NativeArray<PlayerStats>    PlayerStats;

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

                    // Armor reduces incoming damage; always deal at least 1
                    int damage = math.max(1, stats.ContactDamage - PlayerStats[i].Armor);

                    var hp = HealthLookup[PlayerEntities[i]];
                    hp.Current -= damage;
                    inv.Timer   = InvincibilityDuration;

                    HealthLookup[PlayerEntities[i]]     = hp;
                    InvincibleLookup[PlayerEntities[i]] = inv;
                }
            }
        }
    }
}
