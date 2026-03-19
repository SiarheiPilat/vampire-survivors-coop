using Unity.Entities;
using Unity.Mathematics;

namespace VampireSurvivors.Components
{
    /// <summary>
    /// On the thrown Holy Water flask entity.
    /// HolyWaterSystem moves it; on landing (Traveled >= MaxRange) it spawns a
    /// HolyWaterPuddle entity at its position and destroys itself.
    /// </summary>
    public struct HolyWaterProjectile : IComponentData
    {
        public float2 Direction;
        public float  Speed;
        public float  Traveled;
        public float  MaxRange;
        public float  Damage;        // damage passed to the puddle per tick
        public float  PuddleRadius;
        public float  PuddleLifetime;
        public float  TickCooldown;
    }

    /// <summary>
    /// Stationary area-of-effect puddle left by a Holy Water flask.
    /// HolyWaterPuddleSystem ticks damage to all enemies within Radius every TickCooldown s.
    /// Despawns when Lifetime reaches 0.
    /// Wiki base: radius ~1.5 u, damage 20/tick, tick 0.5 s, duration 5 s.
    /// </summary>
    public struct HolyWaterPuddle : IComponentData
    {
        public float Lifetime;
        public float Damage;
        public float Radius;
        public float TickTimer;
        public float TickCooldown;
    }
}
