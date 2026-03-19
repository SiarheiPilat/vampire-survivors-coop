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
    ///   - Levels 2-4: unlocks weapons (MagicWand, Garlic, Knife)
    ///   - Levels 5+:  alternates Spinach (+0.1 Might) and Pummarola (+0.2 HpRegen)
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

                    // Unlock weapons at milestone levels (structural change via ECB)
                    int newLevel = stats.ValueRO.Level;
                    switch (newLevel)
                    {
                        case 2:
                            if (!SystemAPI.HasComponent<MagicWandState>(entity))
                                ecb.AddComponent(entity, new MagicWandState
                                {
                                    Timer = 0f, Cooldown = 0.5f,
                                    Damage = 10f, Speed = 10f, MaxRange = 15f
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
                                    Damage = 10f, Speed = 15f, MaxRange = 12f
                                });
                            break;
                    }

                    // Passive items at level 5+ (alternating Spinach / Pummarola)
                    // Level 5, 7, 9, 11 … → Spinach (+0.1 Might)
                    // Level 6, 8, 10, 12 … → Pummarola (+0.2 HP/s)
                    if (newLevel >= 5)
                    {
                        int pidx = SystemAPI.GetComponent<PlayerIndex>(entity).Value;
                        if (newLevel % 2 == 1) // odd: Spinach
                        {
                            stats.ValueRW.Might += 0.1f;
                            Debug.Log($"[LevelUpSystem] P{pidx} got Spinach! Might = {stats.ValueRO.Might:F1}x");
                        }
                        else // even: Pummarola
                        {
                            stats.ValueRW.HpRegen += 0.2f;
                            Debug.Log($"[LevelUpSystem] P{pidx} got Pummarola! HpRegen = {stats.ValueRO.HpRegen:F1}/s");
                        }
                    }

                    var idx = SystemAPI.GetComponent<PlayerIndex>(entity);
                    Debug.Log($"[LevelUpSystem] P{idx.Value} → Lv{newLevel}! Next: {stats.ValueRO.XpToNextLevel} XP");
                }
            }
        }
    }
}
