using Unity.Entities;
using Unity.Mathematics;

namespace VampireSurvivors.Components
{
    /// <summary>Zero-size marker — identifies all enemy entities.</summary>
    public struct EnemyTag : IComponentData { }

    /// <summary>
    /// Marks an elite/boss enemy. Spawned on a countdown by EnemySpawnerSystem
    /// at boss-timer intervals (starting 45 s, decreasing with wave number).
    /// No split or special death logic — just much higher HP, damage, and XP.
    /// </summary>
    public struct BossTag : IComponentData { }

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
        public bool  IsEvolved;   // true = Bloody Tear: double dmg, heals on hit
        public float HealPerHit;  // HP restored to owner per enemy struck (Bloody Tear)
    }

    /// <summary>
    /// Transient entity created by WhipSystem. Exists for one frame only.
    /// HitArcSystem reads it, applies damage to enemies in range+arc, then destroys it.
    /// Origin stores the player world position at swing time.
    /// </summary>
    public struct HitArc : IComponentData
    {
        public float  Damage;
        public float2 Direction;    // normalised facing direction
        public float  Range;
        public float  ArcDegrees;
        public float3 Origin;       // world position of the swinging player
        public Entity OwnerEntity;  // player entity; Entity.Null = no heal
        public float  HealPerHit;   // HP healed to OwnerEntity per enemy struck (Bloody Tear)
    }

    /// <summary>
    /// Marks a big Slime enemy. On death, HealthSystem spawns 2 SmallSlime entities
    /// at the death position instead of (or in addition to) the normal XP gem.
    /// Wiki: Slime (large) splits into 2 smaller slimes on death.
    /// </summary>
    public struct SlimeTag : IComponentData { }

    /// <summary>
    /// Marks a small Slime (spawned when a big Slime dies).
    /// Does NOT split further on death — behaves like a normal enemy.
    /// </summary>
    public struct SmallSlimeTag : IComponentData { }

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
        public Entity BigSlimePrefab;
        public Entity SmallSlimePrefab;
        public Entity BossPrefab;

        // Pickup prefabs — instantiated by HealthSystem on enemy death
        public Entity XpGemPrefab;
        public Entity GoldCoinPrefab;
        public Entity HealthPickupPrefab;
        public Entity MagnetPickupPrefab;

        public float  Timer;
        public float  BossTimer;   // counts down; spawn boss when ≤ 0
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

    /// <summary>
    /// Transient event entity created by weapon systems when they deal damage to an enemy.
    /// DamageNumberSystem reads these, calls DamageNumberRenderer.Spawn(), then destroys them.
    /// One entity per hit — no pooling at the ECS level (renderer handles the pool).
    /// </summary>
    public struct DamageNumberEvent : IComponentData
    {
        public Unity.Mathematics.float3 WorldPosition;
        public int                      Damage;
    }

    /// <summary>
    /// Knockback impulse applied to enemy entities on hit.
    /// Velocity is added to world position each frame then decayed exponentially
    /// by EnemyMovementSystem (damping ~12/s → fully dissipated in ~0.25 s).
    /// Baked with Velocity = 0 on all enemy prefabs.
    /// Weapon systems write new velocity via ECB on each hit.
    /// </summary>
    public struct Knockback : IComponentData
    {
        public Unity.Mathematics.float2 Velocity; // world units per second
    }
}
