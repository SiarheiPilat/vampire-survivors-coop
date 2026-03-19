using Unity.Entities;

namespace VampireSurvivors.Components
{
    /// <summary>
    /// Placed on each orbiting bible entity by KingBibleSystem.
    /// Stores all per-bible state: owner player, current angle, orbit parameters,
    /// and a per-bible hit cooldown to prevent frame-rate-dependent damage.
    /// </summary>
    public struct KingBibleOrbit : IComponentData
    {
        public Entity Owner;
        public float  Angle;        // current angle in radians
        public float  Radius;       // orbit radius in units
        public float  AngularSpeed; // radians/s
        public float  Damage;
        public float  HitTimer;     // counts down; hit fires when <= 0
        public float  HitCooldown;  // seconds between hits
    }
}
