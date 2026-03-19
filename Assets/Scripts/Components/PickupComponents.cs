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
    /// Singleton — total gold accumulated by the whole team this run.
    /// Created by SharedGoldBootstrapSystem on world startup.
    /// Written by GoldCoinSystem; read by HUDManager for display.
    /// </summary>
    public struct SharedGold : IComponentData
    {
        public int Total;
    }
}
