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
    ///   - Lv2=MagicWand, Lv3=Garlic, Lv4=Knife, Lv5=KingBible, Lv6=FireWand, Lv7=Axe
    ///   - Lv8+: (lv-8)%2==0 → Spinach (+0.1 Might), else → Pummarola (+0.2 HpRegen)
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
                                    Rng      = new Unity.Mathematics.Random((uint)(entity.Index * 2654435761u + 1u))
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
                                    MaxRange = 12f
                                });
                            break;
                    }

                    // Passive items at level 8+ (alternating Spinach / Pummarola)
                    // Level 8, 10, 12 … → Spinach (+0.1 Might)
                    // Level 9, 11, 13 … → Pummarola (+0.2 HP/s)
                    if (newLevel >= 8)
                    {
                        int pidx = SystemAPI.GetComponent<PlayerIndex>(entity).Value;
                        if ((newLevel - 8) % 2 == 0) // lv8, 10, 12… → Spinach
                        {
                            stats.ValueRW.Might += 0.1f;
                            Debug.Log($"[LevelUpSystem] P{pidx} got Spinach! Might = {stats.ValueRO.Might:F1}x");
                        }
                        else // lv9, 11, 13… → Pummarola
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
