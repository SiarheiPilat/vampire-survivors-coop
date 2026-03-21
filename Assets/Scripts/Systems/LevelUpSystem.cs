using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Checks each player's PlayerStats.Xp against XpToNextLevel each frame.
    /// On level-up:
    ///   - Increments Level, resets Xp, updates XpToNextLevel (wiki formula)
    ///   - Grants 2 s invincibility
    ///   - Lv2 MagicWand, Lv3 Garlic, Lv4 Knife, Lv5 KingBible+Bone, Lv6 FireWand,
    ///     Lv7 Axe, Lv8 Cross, Lv9 HolyWater, Lv10 LightningRing
    ///   - Lv11+: (lv-11)%3: 0→Spinach (+0.1 Might), 1→Pummarola (+0.2 HpRegen), 2→Armor (+1)
    /// Weapon systems activate once their state component is present (structural change via ECB).
    /// Not Burst-compiled — calls Debug.Log and uses ECB for structural changes.
    /// </summary>
    [UpdateAfter(typeof(XpGemSystem))]
    public partial struct LevelUpSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (stats, invincible, entity) in
                SystemAPI.Query<RefRW<PlayerStats>, RefRW<Invincible>>()
                    .WithAll<PlayerTag>().WithEntityAccess())
            {
                while (stats.ValueRO.Xp >= stats.ValueRO.XpToNextLevel)
                {
                    stats.ValueRW.Xp           -= stats.ValueRO.XpToNextLevel;
                    stats.ValueRW.Level         += 1;
                    // Wiki formula: XP to next level = 5 + (level-1) * 10
                    stats.ValueRW.XpToNextLevel  = 5f + (stats.ValueRO.Level - 1) * 10f;

                    // Brief invincibility on level-up
                    invincible.ValueRW.Timer = math.max(invincible.ValueRO.Timer, 2f);

                    // Giovanna passive: +1% ProjectileSpeed per level (no cap)
                    if (stats.ValueRO.ProjectileSpeedBonusPerLevel > 0f)
                        stats.ValueRW.ProjectileSpeedMult += stats.ValueRO.ProjectileSpeedBonusPerLevel;

                    // Unlock weapons at milestone levels (structural change via ECB)
                    int newLevel = stats.ValueRO.Level;
                    switch (newLevel)
                    {
                        case 2:
                            if (!SystemAPI.HasComponent<MagicWandState>(entity))
                                ecb.AddComponent(entity, new MagicWandState
                                {
                                    Timer = 0f, Cooldown = 0.5f,
                                    Damage = 10f, Speed = 10f, MaxRange = 15f, Amount = 1
                                });
                            break;
                        case 3:
                            if (!SystemAPI.HasComponent<GarlicState>(entity))
                                ecb.AddComponent(entity, new GarlicState
                                {
                                    Timer = 0f, Cooldown = 1.5f,
                                    Damage = 10f, Range = 1.5f
                                });
                            break;
                        case 4:
                            if (!SystemAPI.HasComponent<KnifeState>(entity))
                                ecb.AddComponent(entity, new KnifeState
                                {
                                    Timer = 0f, Cooldown = 0.35f,
                                    Damage = 10f, Speed = 15f, MaxRange = 12f, Amount = 1
                                });
                            break;
                        case 5:
                            if (!SystemAPI.HasComponent<KingBibleState>(entity))
                                ecb.AddComponent(entity, new KingBibleState
                                {
                                    Damage       = 10f,
                                    Radius       = 1.4f,
                                    AngularSpeed = 2.094f, // ~120°/s in radians
                                    HitCooldown  = 0.5f,
                                    Count        = 1,
                                    Spawned      = false
                                });
                            // Bone also unlocks at level 5 for non-Mortaccio characters
                            if (!SystemAPI.HasComponent<BoneState>(entity))
                                ecb.AddComponent(entity, new BoneState
                                {
                                    Timer    = 0f,
                                    Cooldown = 0.5f,
                                    Damage   = 30f,
                                    Speed    = 8f,
                                    MaxRange = 12f,
                                    Bounces  = 2,
                                    Amount   = 1
                                });
                            break;
                        case 6:
                            if (!SystemAPI.HasComponent<FireWandState>(entity))
                                ecb.AddComponent(entity, new FireWandState
                                {
                                    Timer    = 0f,
                                    Cooldown = 0.4f,
                                    Damage   = 10f,
                                    Speed    = 11f,
                                    MaxRange = 10f,
                                    Rng      = new Unity.Mathematics.Random((uint)(entity.Index * 2654435761u + 1u)),
                                    Amount   = 1
                                });
                            break;
                        case 7:
                            if (!SystemAPI.HasComponent<AxeState>(entity))
                                ecb.AddComponent(entity, new AxeState
                                {
                                    Timer    = 0f,
                                    Cooldown = 1.25f,
                                    Damage   = 20f,
                                    Speed    = 8f,
                                    Gravity  = 12f,
                                    MaxRange = 12f,
                                    Amount   = 1
                                });
                            break;
                        case 8:
                            if (!SystemAPI.HasComponent<CrossState>(entity))
                                ecb.AddComponent(entity, new CrossState
                                {
                                    Timer        = 0f,
                                    Cooldown     = 5.0f,
                                    Damage       = 50f,
                                    Speed        = 15f,
                                    TurnDistance = 8f
                                });
                            break;
                        case 9:
                            if (!SystemAPI.HasComponent<HolyWaterState>(entity))
                                ecb.AddComponent(entity, new HolyWaterState
                                {
                                    Timer          = 0f,
                                    Cooldown       = 6.0f,
                                    Damage         = 20f,
                                    Speed          = 8f,
                                    MaxRange       = 4f,
                                    Radius         = 1.5f,
                                    PuddleLifetime = 5.0f,
                                    TickCooldown   = 0.5f,
                                    Amount         = 1,
                                    Rng            = new Unity.Mathematics.Random((uint)(entity.Index * 1234567891u + 7u))
                                });
                            break;
                        case 10:
                            if (!SystemAPI.HasComponent<LightningRingState>(entity))
                                ecb.AddComponent(entity, new LightningRingState
                                {
                                    Timer    = 0f,
                                    Cooldown = 0.6f,
                                    Damage   = 40f,
                                    Amount   = 1,
                                    Rng      = new Unity.Mathematics.Random((uint)(entity.Index * 987654321u + 3u))
                                });
                            break;
                        // Krochi level 33 bonus: +1 ReviveStock (only if character has ReviveStocks)
                        case 33:
                            if (SystemAPI.HasComponent<ReviveStocks>(entity))
                            {
                                var stocks = SystemAPI.GetComponent<ReviveStocks>(entity);
                                stocks.Count++;
                                ecb.SetComponent(entity, stocks);
                            }
                            break;
                    }

                    // Passive items at level 11+: pause and let the player choose.
                    // HUDManager detects UpgradeChoicePending, pauses time, shows 3 upgrade cards,
                    // applies the chosen stat, then removes UpgradeChoicePending.
                    // We break out of the XP-while-loop so remaining XP is processed
                    // only after the player has made their choice.
                    if (newLevel >= 11)
                    {
                        if (!SystemAPI.HasComponent<UpgradeChoicePending>(entity))
                            ecb.AddComponent<UpgradeChoicePending>(entity);
                        break; // resume next frame once UpgradeChoicePending is removed
                    }

                    var idx = SystemAPI.GetComponent<PlayerIndex>(entity);
                    Debug.Log($"[LevelUpSystem] P{idx.Value} → Lv{newLevel}! Next: {stats.ValueRO.XpToNextLevel} XP");
                }
            }
        }
    }
}
