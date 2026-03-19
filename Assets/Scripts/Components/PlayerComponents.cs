using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace VampireSurvivors.Components
{
    /// <summary>Zero-size marker — identifies all player entities.</summary>
    public struct PlayerTag : IComponentData { }

    /// <summary>
    /// Slot index (0–3) assigned at bake time or by GameSceneBootstrap.
    /// NOT a Gamepad.all index — use AssignedDeviceId for device lookup.
    /// </summary>
    public struct PlayerIndex : IComponentData
    {
        public byte Value;
    }

    /// <summary>
    /// Set by GameSceneBootstrap from GameSession. Stores InputDevice.deviceId
    /// so PlayerInputSystem can look up the correct device regardless of
    /// Gamepad.all connection order.
    /// Value == 0 means "unassigned" — baked entities get this sentinel so the
    /// dev fallback path (Gamepad.all[i]) remains reachable.
    /// </summary>
    public struct AssignedDeviceId : IComponentData
    {
        public int Value; // InputDevice.deviceId; 0 = unassigned
    }

    /// <summary>Current frame's movement input — written by PlayerInputSystem, read by PlayerMovementSystem.</summary>
    public struct MoveInput : IComponentData
    {
        public float2 Value;
    }

    /// <summary>Base movement speed in world units per second.</summary>
    public struct MoveSpeed : IComponentData
    {
        public float Value;
    }

    /// <summary>Player stats — all per-player progression data in one component.</summary>
    public struct PlayerStats : IComponentData
    {
        public int   Hp;
        public int   MaxHp;
        public int   Level;
        public float Xp;
        public float XpToNextLevel;

        /// <summary>
        /// Weapon damage multiplier. Default 1.0. Each Spinach pickup adds +0.1.
        /// Applied by all weapon systems at fire time: finalDamage = baseDamage * Might.
        /// Wiki: Spinach grants +10% Might per level (up to 5 levels in original).
        /// </summary>
        public float Might;

        /// <summary>
        /// HP regenerated per second. 0 = no regen. Each Pummarola pickup adds +0.2.
        /// Wiki base: 0.2 HP/s per Pummarola level.
        /// </summary>
        public float HpRegen;

        /// <summary>Fractional HP accumulator for sub-integer regen per frame.</summary>
        public float HpRegenAccum;
    }

    /// <summary>
    /// Marks a player as downed (HP reached 0). Added by HealthSystem instead of destroying the entity.
    /// Downed players cannot move or attack. Teammates can revive them (future work).
    /// </summary>
    public struct Downed : IComponentData { }

    /// <summary>
    /// Last non-zero movement direction, normalized. Defaults to right (1,0).
    /// Updated by PlayerMovementSystem. Used by Knife and other directional weapons.
    /// </summary>
    public struct FacingDirection : IComponentData
    {
        public float2 Value;
    }

    /// <summary>
    /// Per-player Knife weapon state.
    /// Fires a fast projectile in the player's facing direction.
    /// Wiki base stats: Damage 10, Speed 15 u/s, Cooldown 0.35 s.
    /// </summary>
    public struct KnifeState : IComponentData
    {
        public float Timer;
        public float Cooldown;
        public float Damage;
        public float Speed;
        public float MaxRange;
    }

    /// <summary>
    /// Per-player Garlic aura state.
    /// Pulses every Cooldown seconds, damaging all enemies within Range.
    /// Wiki base stats: Damage 10, Area 1.5 u, Cooldown 1.5 s.
    /// </summary>
    public struct GarlicState : IComponentData
    {
        public float Timer;
        public float Cooldown; // seconds between pulses — wiki base: 1.5s
        public float Damage;   // wiki base: 10
        public float Range;    // radius in units — wiki base: ~1.5
    }

    /// <summary>
    /// Per-player Magic Wand weapon state.
    /// Timer counts down; when it hits 0 a projectile is fired at the nearest enemy
    /// and the timer resets to Cooldown.
    /// </summary>
    public struct MagicWandState : IComponentData
    {
        public float Timer;
        public float Cooldown;  // seconds between shots — wiki base: 0.5s
        public float Damage;    // wiki base: 10
        public float Speed;     // projectile speed in units/s — wiki base: ~10
        public float MaxRange;  // units before projectile despawns
    }

    /// <summary>
    /// Per-player Fire Wand weapon state.
    /// Fires a fireball in a random direction each Cooldown seconds.
    /// Unlike Magic Wand (targets nearest enemy), Fire Wand sprays randomly —
    /// good at clearing crowds but unreliable at range.
    /// Wiki base stats: Damage 10, Speed 11 u/s, Cooldown 0.4 s, MaxRange 10 u.
    /// </summary>
    public struct FireWandState : IComponentData
    {
        public float  Timer;
        public float  Cooldown;
        public float  Damage;
        public float  Speed;
        public float  MaxRange;
        public Random Rng; // per-player RNG so all players fire independently
    }

    /// <summary>
    /// Per-player King Bible weapon state.
    /// Bibles orbit the player permanently, damaging enemies on contact.
    /// KingBibleSystem spawns KingBibleOrbit entities when Spawned == false,
    /// then sets Spawned = true so bibles are only created once.
    /// Wiki base stats: Damage 10, orbit radius ~1.4 u, ~120°/s, 0.5s hit CD per bible.
    /// </summary>
    public struct KingBibleState : IComponentData
    {
        public float Damage;
        public float Radius;       // orbit radius in units
        public float AngularSpeed; // radians/s
        public float HitCooldown;  // seconds between hits per bible
        public int   Count;        // number of orbiting bibles (1 at base)
        public bool  Spawned;      // set to true once bibles are instantiated
    }
}
