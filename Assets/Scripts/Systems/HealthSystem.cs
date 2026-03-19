using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Runs after all damage systems. Destroys any entity whose Health.Current
    /// has dropped to or below 0.
    /// - Enemies: spawns an XpGem entity at the death position, then destroys.
    /// - Players: adds Downed component (entity preserved for revive) — does NOT destroy.
    /// Already-downed players are excluded from this query via WithNone&lt;Downed&gt;.
    /// Not Burst-compiled — calls Debug.Log.
    /// </summary>
    [UpdateAfter(typeof(ContactDamageSystem))]
    [UpdateAfter(typeof(HitArcSystem))]
    public partial struct HealthSystem : ISystem
    {
        ComponentLookup<EnemyStats>    _enemyStatsLookup;
        ComponentLookup<LocalTransform> _transformLookup;

        public void OnCreate(ref SystemState state)
        {
            _enemyStatsLookup = state.GetComponentLookup<EnemyStats>(isReadOnly: true);
            _transformLookup  = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _enemyStatsLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // WithNone<Downed> — skip already-downed players so this doesn't re-trigger
            foreach (var (health, entity) in
                SystemAPI.Query<RefRO<Health>>().WithNone<Downed>().WithEntityAccess())
            {
                if (health.ValueRO.Current > 0) continue;

                if (SystemAPI.HasComponent<EnemyTag>(entity))
                {
                    // Spawn XP gem at enemy's current position, then destroy enemy
                    if (_enemyStatsLookup.HasComponent(entity) && _transformLookup.HasComponent(entity))
                    {
                        var stats     = _enemyStatsLookup[entity];
                        var transform = _transformLookup[entity];

                        var gemEntity = ecb.CreateEntity();
                        ecb.AddComponent(gemEntity, new XpGem { Value = stats.XpValue });
                        ecb.AddComponent(gemEntity, LocalTransform.FromPosition(transform.Position));
                    }
                    ecb.DestroyEntity(entity);
                }
                else if (SystemAPI.HasComponent<PlayerTag>(entity))
                {
                    // Player down — preserve entity for revive, mark as Downed
                    var idx = SystemAPI.GetComponent<PlayerIndex>(entity);
                    Debug.Log($"[HealthSystem] Player {idx.Value} went down.");
                    ecb.AddComponent<Downed>(entity);
                    // Note: entity is NOT destroyed — ReviveSystem (future) removes Downed on revive
                }
                else
                {
                    ecb.DestroyEntity(entity);
                }
            }
        }
    }
}
