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

        /// <summary>
        /// Flat damage reduction applied to every hit before HP is deducted.
        /// damage_taken = max(1, contactDamage - Armor).
        /// Default 0. Each Armor pickup adds +1.
        /// Wiki: Armor reduces all incoming damage by a flat amount.
        /// </summary>
        public int Armor;

        /// <summary>
        /// XP gain multiplier. Default 1.0. Each Crown pickup multiplies by 1.08.
        /// Applied at XP gem collection time: xpGained = gem.Value * XpMult.
        /// Wiki: Crown grants +8% XP gain per level (up to 5 levels in original).
        /// </summary>
        public float XpMult;

        /// <summary>
        /// Weapon cooldown multiplier. Default 1.0. Each Empty Tome pickup multiplies by 0.92.
        /// Applied at weapon fire time: nextTimer = baseCooldown * CooldownMult.
        /// Lower value = shorter cooldown = higher fire rate.
        /// Wiki: Empty Tome grants -8% Cooldown per level (multiplicative, capped at lv5 ≈ 0.659×).
        /// </summary>
        public float CooldownMult;

        /// <summary>
        /// Luck stat. Default 0. Each Clover pickup adds +0.1.
        /// Applied in HealthSystem: dropChance scales by (1 + Luck × nearest-player-luck).
        /// Wiki: Clover increases Luck which affects item find and critical hit chance.
        /// </summary>
        public float Luck;

        /// <summary>
        /// Projectile speed multiplier. Default 1.0. Each Bracer pickup multiplies by 1.1.
        /// Applied by projectile weapon systems at fire time: finalSpeed = baseSpeed * ProjectileSpeedMult.
        /// Wiki: Bracer grants +10% Projectile Speed per level.
        /// </summary>
        public float ProjectileSpeedMult;

        /// <summary>
        /// Flat max-HP bonus accumulated from Hollow Heart pickups.
        /// Stored separately so the evolution condition (MaxHpBonus > 0) can be checked.
        /// Also added to Health.Max and partially to Health.Current at pickup time.
        /// </summary>
        public int MaxHpBonus;

        /// <summary>
        /// Number of Duplicator passives taken. Gate for Thunder Loop evolution.
        /// Each Duplicator pickup adds +1 Amount to every weapon the player owns.
        /// Wiki: Duplicator — +1 Amount per level, max 2 levels.
        /// </summary>
        public int DuplicatorStacks;

        /// <summary>
        /// Area-of-effect size multiplier. Default 1.0. Each Candelabrador pickup multiplies by 1.1.
        /// Applied to: Garlic range, Whip hit arc range, Holy Water puddle radius, King Bible orbit radius.
        /// Wiki: Candelabrador grants +10% Area per level.
        /// </summary>
        public float AreaMult;

        /// <summary>
        /// Effect duration multiplier. Default 1.0. Each Spellbinder pickup multiplies by 1.1.
        /// Applied to: Holy Water puddle lifetime.
        /// Wiki: Spellbinder grants +10% Duration per level.
        /// </summary>
        public float DurationMult;

        /// <summary>
        /// Movement speed multiplier. Default 1.0. Each Wings pickup adds 0.1 (additive).
        /// Applied to: PlayerMovementSystem (multiplies MoveSpeed.Value).
        /// Wiki: Wings grants +10% Move Speed per level, max 5 levels.
        /// </summary>
        public float SpeedMult;

        /// <summary>
        /// XP magnet radius multiplier. Default 1.0. Each Attractorb pickup multiplies by 1.3.
        /// Applied to: XpGemSystem magnet range (base 30u × MagnetRadiusMult).
        /// Wiki: Attractorb increases item pickup range, roughly ×1.5/×2/×2.5/×3/×4 over 5 levels.
        /// </summary>
        public float MagnetRadiusMult;

        /// <summary>
        /// Flat projectile speed bonus added to ProjectileSpeedMult each level-up.
        /// Default 0.0. Giovanna gets 0.01 (+1% ProjectileSpeed per level, no cap).
        /// Wiki: Giovanna — projectile speed increases by 1% each level.
        /// </summary>
        public float ProjectileSpeedBonusPerLevel;

        /// <summary>
        /// Gold earnings multiplier. Default 1.0. Each Stone Mask pickup adds +0.1 (additive, max +0.5 at lv5).
        /// Applied in GoldCoinSystem: goldEarned = coin.Value * GoldMult (rounded).
        /// Wiki: Stone Mask grants +10% Greed (coin earnings) per level, up to 5 levels.
        /// </summary>
        public float GoldMult;
    }

    /// <summary>
    /// Krochi's signature: automatic self-revive stocks.
    /// When HP drops to 0 and Count > 0, HealthSystem auto-revives the player (50% HP, 3s iframes)
    /// instead of applying Downed. Count starts at 1 for Krochi, +1 more at level 33.
    /// </summary>
    public struct ReviveStocks : IComponentData
    {
        public int Count; // remaining auto-revives
    }

    /// <summary>
    /// Added to a player entity when a passive-item level-up is pending player input.
    /// LevelUpSystem stops auto-granting and breaks its XP loop.
    /// HUDManager detects this, pauses time, and shows upgrade choice cards.
    /// Removed by HUDManager once the player makes a choice.
    /// </summary>
    public struct UpgradeChoicePending : IComponentData { }

    /// <summary>
    /// Marks a player as downed (HP reached 0). Added by HealthSystem instead of destroying the entity.
    /// Downed players cannot move or attack. Teammates revive them by holding Interact nearby.
    /// </summary>
    public struct Downed : IComponentData { }

    /// <summary>
    /// Added to a downed player entity while a teammate is actively holding Interact nearby.
    /// Timer counts up; when it reaches ReviveSystem.ReviveDuration the player is revived.
    /// Removed when the reviver moves away, releases Interact, or the revive completes.
    /// </summary>
    public struct ReviveProgress : IComponentData
    {
        public Entity Reviver; // living player entity performing the revive
        public float  Timer;   // seconds held so far
    }

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
        public int   Amount;     // knives per throw; 1 = default; fan spread, 20° between blades
        public bool  IsEvolved;  // true = Thousand Edge: 5 knives, speed 20, 0.15s CD, 15 dmg
    }

    /// <summary>
    /// Per-player Garlic aura state.
    /// Pulses every Cooldown seconds, damaging all enemies within Range.
    /// Wiki base stats: Damage 10, Area 1.5 u, Cooldown 1.5 s.
    /// </summary>
    public struct GarlicState : IComponentData
    {
        public float Timer;
        public float Cooldown;     // seconds between pulses — wiki base: 1.5s
        public float Damage;       // wiki base: 10
        public float Range;        // radius in units — wiki base: ~1.5
        public bool  IsEvolved;    // true = Soul Eater: Range 3.5u, 25 dmg, heals owner
        public float HealPerPulse; // HP healed to the owning player per pulse (Soul Eater only)
    }

    /// <summary>
    /// Per-player Magic Wand weapon state.
    /// Timer counts down; when it hits 0 a projectile is fired at the nearest enemy
    /// and the timer resets to Cooldown.
    /// </summary>
    public struct MagicWandState : IComponentData
    {
        public float Timer;
        public float Cooldown;   // seconds between shots — wiki base: 0.5s
        public float Damage;     // wiki base: 10
        public float Speed;      // projectile speed in units/s — wiki base: ~10
        public float MaxRange;   // units before projectile despawns
        public int   Amount;     // projectiles per shot; 1 = default; upgrade adds +1 (fan spread, 20° between shots)
        public bool  IsEvolved;  // true = Holy Wand: 7 shots, 20 dmg, 0.25 s CD, 10° spread
    }

    /// <summary>
    /// Per-player Lightning Ring weapon state.
    /// Instant-hits up to Amount random enemies every Cooldown seconds.
    /// No projectile — damage is applied immediately to selected targets.
    /// Wiki base stats: Damage 40, Cooldown 0.6 s, Amount 1 enemy/strike.
    /// </summary>
    public struct LightningRingState : IComponentData
    {
        public float  Timer;
        public float  Cooldown;
        public float  Damage;
        public int    Amount;     // enemies struck per activation
        public bool   IsEvolved;  // true = Thunder Loop: 65 dmg, 0.5s CD, 6 targets
        public Random Rng;
    }

    /// <summary>
    /// Per-player Holy Water weapon state.
    /// Throws a flask in a random direction; on landing it creates a stationary
    /// damage puddle that ticks all enemies within Radius every TickCooldown seconds.
    /// Wiki base stats: Damage 20/tick, Cooldown 6.0 s, Radius 1.5 u,
    ///   TickCooldown 0.5 s, PuddleLifetime 5.0 s.
    /// </summary>
    public struct HolyWaterState : IComponentData
    {
        public float  Timer;
        public float  Cooldown;
        public float  Damage;
        public float  Speed;          // flask travel speed u/s
        public float  MaxRange;       // flask travel distance before landing
        public float  Radius;         // puddle radius
        public float  PuddleLifetime;
        public float  TickCooldown;
        public int    Amount;    // flasks thrown per cooldown (1=default); each in independent random direction
        public Random Rng;
        /// <summary>True = La Borra (Holy Water + Attractorb): puddles follow player, 2× radius, 40 dmg.</summary>
        public bool   IsEvolved;
    }

    /// <summary>
    /// Per-player Cross weapon state.
    /// Fires a cross in the player's facing direction. The projectile travels out to
    /// TurnDistance, then reverses and homes back to the player (Projectile.Returning).
    /// High damage, slow cooldown — reward for staying still and letting it orbit back.
    /// Wiki base stats: Damage 50, Cooldown 5.0 s, Speed 15 u/s.
    /// </summary>
    public struct CrossState : IComponentData
    {
        public float Timer;
        public float Cooldown;      // wiki: 5.0 s
        public float Damage;        // wiki: 50
        public float Speed;         // u/s
        public float TurnDistance;  // units before reversing
        public bool  IsEvolved;     // true = Heaven Sword: 2 swords, 200 dmg, 2.5s CD, piercing, no return
        public int   Count;         // swords per volley (1 base, 2 when evolved)
    }

    /// <summary>
    /// Per-player Axe weapon state.
    /// Fires a heavy axe in a parabolic arc (upward then falling).
    /// Uses Projectile.Gravity/Velocity for arcing movement.
    /// Wiki base stats: Damage 20, Cooldown 1.25 s.
    /// </summary>
    public struct AxeState : IComponentData
    {
        public float Timer;
        public float Cooldown;  // wiki: 1.25 s
        public float Damage;    // wiki: 20
        public float Speed;     // launch speed (u/s)
        public float Gravity;   // downward accel (u/s²)
        public float MaxRange;  // path length before despawn
        public int   Amount;    // axes fired per cooldown (1=default); fan spread 20° between axes
        /// <summary>True after Death Spiral evolution. Fires 9 piercing scythes radially instead of arcing axes.</summary>
        public bool  IsEvolved;
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
        public int    Amount; // fireballs per trigger; 1 = default; each fires in independent random direction
        /// <summary>True = O'Sole Meeo (Fire Wand + Candelabrador): 8 fireballs, 20 dmg.</summary>
        public bool   IsEvolved;
        /// <summary>True = Hellfire (Fire Wand + Spinach): 2 slow piercing meteors, 100 dmg, 3s CD.</summary>
        public bool   IsHellfire;
    }

    /// <summary>
    /// Per-player Runetracer weapon state.
    /// Fires a projectile in the player's facing direction that bounces off
    /// virtual walls (when MaxRange is exceeded, direction reflects off the
    /// nearest axis-aligned surface) up to BounceCount times.
    /// Wiki base stats: Damage 10, Speed 8 u/s, Cooldown 0.35 s, Bounces 3.
    /// </summary>
    public struct RunetracerState : IComponentData
    {
        public float Timer;
        public float Cooldown;
        public float Damage;
        public float Speed;
        public float MaxRange; // distance per bounce segment (screen-width approximation)
        public byte  Bounces;  // wall bounces granted per projectile
        public int   Amount;   // projectiles fired per cooldown (1=default); fan spread 20° between
        /// <summary>True after NO FUTURE evolution. Projectiles explode on final expire.</summary>
        public bool  IsEvolved;
    }

    /// <summary>
    /// Per-player Bone weapon state (Mortaccio's starter weapon).
    /// Fires a bone in the facing direction. The bone bounces off virtual walls
    /// up to Bounces times (like Runetracer, but higher damage, fewer bounces).
    /// Wiki base stats: Damage 30, Cooldown 0.5 s, Speed 8 u/s, Bounces 2, MaxRange 12 u.
    /// </summary>
    public struct BoneState : IComponentData
    {
        public float Timer;
        public float Cooldown;  // wiki: 0.5 s
        public float Damage;    // wiki: 30
        public float Speed;     // u/s, wiki: 8
        public float MaxRange;  // units per bounce segment
        public byte  Bounces;   // wall bounces per projectile (wiki: 2)
        public int   Amount;    // bones fired per cooldown (1=default); fan spread 20° between bones
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
        public bool  IsEvolved;    // true = Unholy Vespers: Damage=30, Radius=1.75, Count=3
    }

    /// <summary>
    /// Per-player Gatti Amari weapon state (Giovanna's starter).
    /// Summons wandering cats that randomly attack nearby enemies in a small AoE.
    /// Wiki base stats: Damage 10, Cooldown 5.0 s, CatLifetime 5.0 s, Amount 1.
    /// </summary>
    public struct GattiAmariState : IComponentData
    {
        public float Timer;
        public float Cooldown;      // seconds between cat spawns — wiki base: 5.0s
        public float Damage;        // wiki base: 10
        public float CatLifetime;   // seconds each cat lives — wiki base: 5.0s
        public int   Amount;        // cats spawned per cooldown (1=default); upgradeable up to 3
        /// <summary>True after Vicious Hunger evolution (Gatti Amari + Stone Mask): 30 dmg, 8s CD, 2 giant cats, 7s lifetime.</summary>
        public bool  IsEvolved;
    }
}
