using Unity.Entities;
using Unity.Mathematics;

namespace VampireSurvivors.Components
{
    /// <summary>
    /// Projectile fired by ranged weapons (Magic Wand, Knife, Fire Wand, …).
    /// ProjectileMovementSystem moves it each frame; ProjectileHitSystem checks
    /// enemy overlap and destroys it on first hit.
    /// </summary>
    public struct Projectile : IComponentData
    {
        public float  Damage;
        public float  Speed;
        public float3 Direction;  // normalised world-space direction
        public float  MaxRange;
        public float  Traveled;
    }
}
