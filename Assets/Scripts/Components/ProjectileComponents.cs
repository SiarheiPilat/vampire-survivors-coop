using Unity.Entities;
using Unity.Mathematics;

namespace VampireSurvivors.Components
{
    /// <summary>
    /// Projectile fired by ranged weapons (Magic Wand, Knife, Fire Wand, Axe, …).
    /// ProjectileMovementSystem moves it each frame; ProjectileHitSystem checks
    /// enemy overlap and destroys it on first hit.
    ///
    /// Straight projectiles: set Gravity = 0, Direction = normalised heading.
    ///   Movement: position += Direction * Speed * dt
    ///
    /// Arcing projectiles (Axe): set Gravity > 0, Velocity = initial velocity vector.
    ///   Movement: Velocity.y -= Gravity * dt; position += Velocity * dt
    /// </summary>
    public struct Projectile : IComponentData
    {
        public float  Damage;
        public float  Speed;
        public float3 Direction;  // normalised heading (used when Gravity == 0)
        public float  MaxRange;
        public float  Traveled;

        /// <summary>Downward acceleration in u/s². 0 = straight line (default).</summary>
        public float  Gravity;

        /// <summary>
        /// Current velocity vector for arcing projectiles.
        /// Initialised to the launch vector by the firing system; updated each frame.
        /// Ignored (zero) for straight projectiles.
        /// </summary>
        public float3 Velocity;
    }
}
