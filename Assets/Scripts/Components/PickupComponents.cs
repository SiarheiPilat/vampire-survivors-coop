using Unity.Entities;

namespace VampireSurvivors.Components
{
    /// <summary>
    /// Gold coin dropped by enemies on death. Shared between all players.
    /// Players collect by walking within CollectRadius. Value added to SharedGold.Total.
    /// Wiki: coins add to the player's gold pool; used for post-run upgrades.
    /// </summary>
    public struct GoldCoin : IComponentData
    {
        public int Value;
    }

    /// <summary>
    /// Health pickup (cross) dropped rarely by enemies.
    /// Collected by the first player to walk within CollectRadius.
    /// Restores HealAmount HP capped at Health.Max.
    /// </summary>
    public struct HealthPickup : IComponentData
    {
        public int HealAmount;
    }

    /// <summary>
    /// Magnet item dropped rarely by enemies. When collected, immediately vacuums
    /// all XP gems on screen to the collecting player (full XP credited, gems destroyed).
    /// Effect is instant — no duration. Wiki: Attractorb / floor magnet item.
    /// </summary>
    public struct MagnetPickup : IComponentData { }

    /// <summary>
    /// Treasure chest dropped by enemies. When collected by a player it awards one
    /// of four rewards determined by RNG at the moment of collection:
    ///   40% → Gold bonus (100–200 gold to team pool)
    ///   30% → Full HP restore for collecting player
    ///   20% → XP burst (+100 XP for collecting player)
    ///   10% → Invincibility surge (8 seconds for collecting player)
    /// RNG is seeded at spawn time so each chest has independent variance.
    /// </summary>
    public struct Chest : IComponentData
    {
        public Unity.Mathematics.Random Rng;
    }

    /// <summary>
    /// Singleton — team-wide run statistics.
    /// Created by SharedGoldBootstrapSystem on world startup.
    /// Written by GoldCoinSystem (Total) and HealthSystem (EnemiesKilled).
    /// Read by HUDManager for gold display and game-over stats screen.
    /// </summary>
    public struct SharedGold : IComponentData
    {
        public int Total;         // gold accumulated by the team
        public int EnemiesKilled; // total enemies destroyed this run
    }
}
