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
    ///   - Unlocks new weapon components at specific level thresholds:
    ///       Level 2 → MagicWandState
    ///       Level 3 → GarlicState
    ///       Level 4 → KnifeState
    /// Weapon systems activate automatically once their state component is present.
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

                    var idx = SystemAPI.GetComponent<PlayerIndex>(entity);
                    Debug.Log($"[LevelUpSystem] P{idx.Value} → Lv{newLevel}! XP needed: {stats.ValueRO.XpToNextLevel}");
                }
            }
        }
    }
}
