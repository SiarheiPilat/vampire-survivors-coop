using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Checks each player's PlayerStats.Xp against XpToNextLevel.
    /// When threshold is met, increments Level, resets Xp, updates XpToNextLevel,
    /// and grants 2 seconds of invincibility.
    /// Level-up UI (weapon choice) is future work.
    /// Not Burst-compiled — calls Debug.Log.
    /// </summary>
    [UpdateAfter(typeof(XpGemSystem))]
    public partial struct LevelUpSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (stats, invincible, entity) in
                SystemAPI.Query<RefRW<PlayerStats>, RefRW<Invincible>>()
                    .WithAll<PlayerTag>().WithEntityAccess())
            {
                while (stats.ValueRO.Xp >= stats.ValueRO.XpToNextLevel)
                {
                    stats.ValueRW.Xp           -= stats.ValueRO.XpToNextLevel;
                    stats.ValueRW.Level         += 1;
                    // Wiki formula: XP to next level = 5 + (level-1) * 10, for levels 1-20
                    stats.ValueRW.XpToNextLevel  = 5f + (stats.ValueRO.Level - 1) * 10f;

                    // Brief invincibility on level-up
                    invincible.ValueRW.Timer = math.max(invincible.ValueRO.Timer, 2f);

                    var idx = SystemAPI.GetComponent<PlayerIndex>(entity);
                    Debug.Log($"[LevelUpSystem] Player {idx.Value} reached level {stats.ValueRO.Level}! Next level: {stats.ValueRO.XpToNextLevel} XP");
                }
            }
        }
    }
}
