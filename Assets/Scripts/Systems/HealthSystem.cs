using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Runs after all damage systems. Destroys any entity whose Health.Current
    /// has dropped to or below 0. Logs a message for player deaths.
    /// Spawns an XpGem entity at the position of each destroyed enemy.
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

            foreach (var (health, entity) in
                SystemAPI.Query<RefRO<Health>>().WithEntityAccess())
            {
                if (health.ValueRO.Current > 0) continue;

                if (SystemAPI.HasComponent<EnemyTag>(entity))
                {
                    // Spawn XP gem at enemy's current position
                    if (_enemyStatsLookup.HasComponent(entity) && _transformLookup.HasComponent(entity))
                    {
                        var stats     = _enemyStatsLookup[entity];
                        var transform = _transformLookup[entity];

                        var gemEntity = ecb.CreateEntity();
                        ecb.AddComponent(gemEntity, new XpGem { Value = stats.XpValue });
                        ecb.AddComponent(gemEntity, LocalTransform.FromPosition(transform.Position));
                    }
                }
                else if (SystemAPI.HasComponent<PlayerTag>(entity))
                {
                    var idx = SystemAPI.GetComponent<PlayerIndex>(entity);
                    Debug.Log($"[HealthSystem] Player {idx.Value} died.");
                }

                ecb.DestroyEntity(entity);
            }
        }
    }
}
