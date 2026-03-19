using Unity.Entities;
using Unity.Mathematics;

namespace VampireSurvivors.Components
{
    /// <summary>Zero-size marker — identifies all enemy entities.</summary>
    public struct EnemyTag : IComponentData { }

    /// <summary>Enemy movement and combat stats.</summary>
    public struct EnemyStats : IComponentData
    {
        public float MoveSpeed;
        public int ContactDamage;
        public int XpValue;
    }

    /// <summary>
    /// Current and maximum hit points. Added to both enemy and player entities.
    /// PlayerStats.Hp/MaxHp remain untouched (used for leveling later).
    /// </summary>
    public struct Health : IComponentData
    {
        public int Current;
        public int Max;
    }

    /// <summary>
    /// Contact-damage cooldown. When Timer > 0 the entity cannot take contact damage.
    /// Ticked down by InvincibilitySystem. Added to player entities only.
    /// </summary>
    public struct Invincible : IComponentData
    {
        public float Timer;
    }

    /// <summary>
    /// Whip weapon state on player entities. SwingTimer counts down; when it hits 0 a
    /// HitArc entity is spawned and the timer resets to SwingCooldown.
    /// </summary>
    public struct WeaponState : IComponentData
    {
        public float SwingTimer;
        public float SwingCooldown;
        public float Damage;
        public float Range;
        public float ArcDegrees;
    }

    /// <summary>
    /// Transient entity created by WhipSystem. Exists for one frame only.
    /// HitArcSystem reads it, applies damage to enemies in range+arc, then destroys it.
    /// Origin stores the player world position at swing time.
    /// </summary>
    public struct HitArc : IComponentData
    {
        public float Damage;
        public float2 Direction;   // normalised facing direction
        public float Range;
        public float ArcDegrees;
        public float3 Origin;      // world position of the swinging player
    }

    /// <summary>
    /// Singleton component on the spawner entity. Baked by SpawnerAuthoring.
    /// Holds entity prefab references and mutable spawner state (timer + RNG).
    /// </summary>
    public struct SpawnerData : IComponentData
    {
        public Entity BatPrefab;
        public Entity ZombiePrefab;
        public Entity SkeletonPrefab;
        public float Timer;
        public Unity.Mathematics.Random Rng;
    }

    /// <summary>
    /// Marks an XP gem entity. Spawned at enemy death positions by HealthSystem.
    /// XpGemSystem moves gems toward players in magnet radius and collects them on contact.
    /// </summary>
    public struct XpGem : IComponentData
    {
        public float Value;
    }
}
