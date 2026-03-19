using Unity.Entities;
using Unity.Mathematics;
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
                    // Big Slime splits into 2 SmallSlimes before the normal death loot
                    bool isSlime = SystemAPI.HasComponent<SlimeTag>(entity);
                    if (isSlime && _transformLookup.HasComponent(entity) &&
                        SystemAPI.TryGetSingleton<SpawnerData>(out var spawnerData) &&
                        spawnerData.SmallSlimePrefab != Entity.Null)
                    {
                        var pos = _transformLookup[entity].Position;
                        for (int s = 0; s < 2; s++)
                        {
                            var sm = ecb.Instantiate(spawnerData.SmallSlimePrefab);
                            ecb.SetComponent(sm, LocalTransform.FromPosition(
                                pos + new float3(s == 0 ? -0.4f : 0.4f, 0.2f, 0f)));
                        }
                    }

                    // Spawn XP gem, gold coin, and (rarely) a health pickup at death position
                    if (_enemyStatsLookup.HasComponent(entity) && _transformLookup.HasComponent(entity))
                    {
                        var stats     = _enemyStatsLookup[entity];
                        var transform = _transformLookup[entity];

                        // XP gem
                        var gemEntity = ecb.CreateEntity();
                        ecb.AddComponent(gemEntity, new XpGem { Value = stats.XpValue });
                        ecb.AddComponent(gemEntity, LocalTransform.FromPosition(transform.Position));

                        // Gold coin (always) — value scales with XP worth of the enemy
                        int goldValue  = UnityEngine.Mathf.Max(1, stats.XpValue / 2);
                        var coinEntity = ecb.CreateEntity();
                        ecb.AddComponent(coinEntity, new GoldCoin { Value = goldValue });
                        ecb.AddComponent(coinEntity, LocalTransform.FromPosition(
                            transform.Position + new float3(-0.3f, 0.3f, 0f)));

                        // Health pickup (~10% chance) — restores 30 HP to the collector
                        if (UnityEngine.Random.value < 0.10f)
                        {
                            var healEntity = ecb.CreateEntity();
                            ecb.AddComponent(healEntity, new HealthPickup { HealAmount = 30 });
                            ecb.AddComponent(healEntity, LocalTransform.FromPosition(
                                transform.Position + new float3(0.3f, -0.3f, 0f)));
                        }

                        // Magnet pickup (~3% chance) — vacuums all XP gems on screen
                        if (UnityEngine.Random.value < 0.03f)
                        {
                            var magEntity = ecb.CreateEntity();
                            ecb.AddComponent(magEntity, new MagnetPickup());
                            ecb.AddComponent(magEntity, LocalTransform.FromPosition(
                                transform.Position + new float3(0f, 0.5f, 0f)));
                        }
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
