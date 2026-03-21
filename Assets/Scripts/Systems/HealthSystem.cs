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

            // Grab run-stats singleton so we can count kills (updated directly on main thread)
            bool hasRunStats = SystemAPI.TryGetSingletonEntity<SharedGold>(out var runStatsEntity);
            var  runStats    = hasRunStats ? SystemAPI.GetSingleton<SharedGold>() : default;

            // Average Luck across living players — scales enemy drop probabilities
            float avgLuck = 0f;
            int   playerCount = 0;
            foreach (var stats in SystemAPI.Query<RefRO<PlayerStats>>()
                .WithAll<PlayerTag>().WithNone<Downed>())
            {
                avgLuck += stats.ValueRO.Luck;
                playerCount++;
            }
            if (playerCount > 0) avgLuck /= playerCount;
            float luckMult = 1f + avgLuck; // Luck=0 → 1×, Luck=0.1 → 1.1×, Luck=1.0 → 2×

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

                    // Spawn XP gem, gold coin, and (rarely) a health/magnet pickup at death position
                    if (_enemyStatsLookup.HasComponent(entity) && _transformLookup.HasComponent(entity))
                    {
                        var enemyStats = _enemyStatsLookup[entity];
                        var transform  = _transformLookup[entity];
                        bool hasPrefabs = SystemAPI.TryGetSingleton<SpawnerData>(out var pickupSpawner);

                        // XP gem — use prefab for visuals if available, otherwise plain entity
                        if (hasPrefabs && pickupSpawner.XpGemPrefab != Entity.Null)
                        {
                            var gem = ecb.Instantiate(pickupSpawner.XpGemPrefab);
                            ecb.SetComponent(gem, new XpGem { Value = enemyStats.XpValue });
                            ecb.SetComponent(gem, LocalTransform.FromPosition(transform.Position));
                        }
                        else
                        {
                            var gem = ecb.CreateEntity();
                            ecb.AddComponent(gem, new XpGem { Value = enemyStats.XpValue });
                            ecb.AddComponent(gem, LocalTransform.FromPosition(transform.Position));
                        }

                        // Gold coin (always) — value scales with XP worth of the enemy
                        int goldValue = UnityEngine.Mathf.Max(1, enemyStats.XpValue / 2);
                        if (hasPrefabs && pickupSpawner.GoldCoinPrefab != Entity.Null)
                        {
                            var coin = ecb.Instantiate(pickupSpawner.GoldCoinPrefab);
                            ecb.SetComponent(coin, new GoldCoin { Value = goldValue });
                            ecb.SetComponent(coin, LocalTransform.FromPosition(
                                transform.Position + new float3(-0.3f, 0.3f, 0f)));
                        }
                        else
                        {
                            var coin = ecb.CreateEntity();
                            ecb.AddComponent(coin, new GoldCoin { Value = goldValue });
                            ecb.AddComponent(coin, LocalTransform.FromPosition(
                                transform.Position + new float3(-0.3f, 0.3f, 0f)));
                        }

                        // Health pickup (~10% base chance, scaled by team Luck) — restores 30 HP
                        if (UnityEngine.Random.value < 0.10f * luckMult)
                        {
                            if (hasPrefabs && pickupSpawner.HealthPickupPrefab != Entity.Null)
                            {
                                var heal = ecb.Instantiate(pickupSpawner.HealthPickupPrefab);
                                ecb.SetComponent(heal, new HealthPickup { HealAmount = 30 });
                                ecb.SetComponent(heal, LocalTransform.FromPosition(
                                    transform.Position + new float3(0.3f, -0.3f, 0f)));
                            }
                            else
                            {
                                var heal = ecb.CreateEntity();
                                ecb.AddComponent(heal, new HealthPickup { HealAmount = 30 });
                                ecb.AddComponent(heal, LocalTransform.FromPosition(
                                    transform.Position + new float3(0.3f, -0.3f, 0f)));
                            }
                        }

                        // Magnet pickup (~3% base chance, scaled by team Luck) — vacuums all XP gems
                        if (UnityEngine.Random.value < 0.03f * luckMult)
                        {
                            if (hasPrefabs && pickupSpawner.MagnetPickupPrefab != Entity.Null)
                            {
                                var mag = ecb.Instantiate(pickupSpawner.MagnetPickupPrefab);
                                ecb.SetComponent(mag, LocalTransform.FromPosition(
                                    transform.Position + new float3(0f, 0.5f, 0f)));
                            }
                            else
                            {
                                var mag = ecb.CreateEntity();
                                ecb.AddComponent(mag, new MagnetPickup());
                                ecb.AddComponent(mag, LocalTransform.FromPosition(
                                    transform.Position + new float3(0f, 0.5f, 0f)));
                            }
                        }

                        // Chest — always for elites; 5% base chance otherwise (scaled by Luck)
                        bool isElite      = SystemAPI.HasComponent<EliteTag>(entity);
                        bool spawnChest   = isElite ||
                                            (UnityEngine.Random.value < 0.05f * luckMult);
                        if (spawnChest &&
                            hasPrefabs && pickupSpawner.ChestPrefab != Entity.Null)
                        {
                            var chest = ecb.Instantiate(pickupSpawner.ChestPrefab);
                            ecb.SetComponent(chest, LocalTransform.FromPosition(
                                transform.Position + new float3(0.3f, 0.5f, 0f)));
                            ecb.SetComponent(chest, new Chest
                            {
                                Rng = Unity.Mathematics.Random.CreateFromIndex(
                                    (uint)(transform.Position.x * 1000f + transform.Position.y * 37f +
                                           (uint)runStats.EnemiesKilled))
                            });
                        }

                        // Bomb pickup (~1% base chance, scaled by Luck) — AoE 80 dmg in 3u on collect
                        if (UnityEngine.Random.value < 0.01f * luckMult)
                        {
                            if (hasPrefabs && pickupSpawner.BombPickupPrefab != Entity.Null)
                            {
                                var bomb = ecb.Instantiate(pickupSpawner.BombPickupPrefab);
                                ecb.SetComponent(bomb, LocalTransform.FromPosition(
                                    transform.Position + new float3(0.4f, 0.3f, 0f)));
                            }
                            else
                            {
                                var bomb = ecb.CreateEntity();
                                ecb.AddComponent(bomb, new BombPickup());
                                ecb.AddComponent(bomb, LocalTransform.FromPosition(
                                    transform.Position + new float3(0.4f, 0.3f, 0f)));
                            }
                        }

                        // Orologion (~1.5% base chance, scaled by Luck) — freezes all enemies 10s
                        if (UnityEngine.Random.value < 0.015f * luckMult)
                        {
                            if (hasPrefabs && pickupSpawner.OrologionPickupPrefab != Entity.Null)
                            {
                                var orolo = ecb.Instantiate(pickupSpawner.OrologionPickupPrefab);
                                ecb.SetComponent(orolo, LocalTransform.FromPosition(
                                    transform.Position + new float3(-0.3f, -0.5f, 0f)));
                            }
                            else
                            {
                                var orolo = ecb.CreateEntity();
                                ecb.AddComponent(orolo, new OrologionPickup());
                                ecb.AddComponent(orolo, LocalTransform.FromPosition(
                                    transform.Position + new float3(-0.3f, -0.5f, 0f)));
                            }
                        }
                    }
                    if (hasRunStats) runStats.EnemiesKilled++;
                    ecb.DestroyEntity(entity);
                }
                else if (SystemAPI.HasComponent<PlayerTag>(entity))
                {
                    var idx = SystemAPI.GetComponent<PlayerIndex>(entity);

                    // Krochi auto-revive: consume a ReviveStock instead of going Downed
                    bool autoRevived = false;
                    if (SystemAPI.HasComponent<ReviveStocks>(entity))
                    {
                        var stocks = SystemAPI.GetComponent<ReviveStocks>(entity);
                        if (stocks.Count > 0)
                        {
                            stocks.Count--;
                            ecb.SetComponent(entity, stocks);

                            // Restore 50% max HP
                            var hp   = SystemAPI.GetComponent<Health>(entity);
                            hp.Current = math.max(1, hp.Max / 2);
                            ecb.SetComponent(entity, hp);

                            // 3 seconds of invincibility
                            var inv = SystemAPI.GetComponent<Invincible>(entity);
                            inv.Timer = math.max(inv.Timer, 3f);
                            ecb.SetComponent(entity, inv);

                            Debug.Log($"[HealthSystem] P{idx.Value} auto-revived via ReviveStock (remaining: {stocks.Count})");
                            autoRevived = true;
                        }
                    }

                    if (!autoRevived)
                    {
                        Debug.Log($"[HealthSystem] Player {idx.Value} went down.");
                        ecb.AddComponent<Downed>(entity);
                    }
                }
                else
                {
                    if (hasRunStats) runStats.EnemiesKilled++;
                    ecb.DestroyEntity(entity);
                }
            }

            // Write kill count back to the singleton
            if (hasRunStats)
                state.EntityManager.SetComponentData(runStatsEntity, runStats);
        }
    }
}
