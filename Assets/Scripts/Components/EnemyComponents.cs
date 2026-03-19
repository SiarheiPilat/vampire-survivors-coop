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
    /// Holds entity prefab references and mutable spawner state (timer, RNG, wave).
    ///
    /// Wave scaling: every 30 s a new wave starts.
    ///   StatMultiplier = 1 + (wave-1) * 0.2  → wave 5 = 2× stats
    ///   Spawn count grows by 1 each wave (capped at 18).
    ///   Spawn interval shrinks by 0.15 s each wave (floor 1.5 s).
    /// </summary>
    public struct SpawnerData : IComponentData
    {
        public Entity BatPrefab;
        public Entity ZombiePrefab;
        public Entity SkeletonPrefab;
        public float  Timer;
        public Unity.Mathematics.Random Rng;

        /// <summary>Total elapsed play time in seconds. Drives wave number.</summary>
        public float ElapsedTime;

        /// <summary>Current wave number (1-based). Increments every 30 s.</summary>
        public int   WaveNumber;

        /// <summary>Enemy stat multiplier for this wave (HP, damage, XP scale together).</summary>
        public float StatMultiplier;
    }

    /// <summary>
    /// Singleton — baked by BulletPrefabAuthoring.
    /// Holds the entity prefab reference used by projectile weapons (Magic Wand, Knife, …).
    /// </summary>
    public struct BulletPrefabData : IComponentData
    {
        public Entity BulletPrefab;
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
