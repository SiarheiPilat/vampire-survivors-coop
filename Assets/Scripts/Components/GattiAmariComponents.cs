using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace VampireSurvivors.Components
{
    /// <summary>
    /// Marks a cat entity spawned by Gatti Amari.
    /// Cats wander randomly and attack nearby enemies every AttackCooldown seconds.
    /// Wiki: cats wander at ~1 u/s, attack enemies within small AoE radius.
    /// </summary>
    public struct GattiAmariCat : IComponentData
    {
        public float  Damage;          // wiki: 10 base (scales with Might at spawn time)
        public float  Radius;          // attack AoE radius: 0.5u (scales with AreaMult at spawn)
        public float  Lifetime;        // seconds remaining before cat despawns
        public float  AttackTimer;     // countdown to next attack
        public float  AttackCooldown;  // seconds between attacks — wiki: ~1.0s
        public float  WanderTimer;     // countdown to next direction change
        public float2 WanderDir;       // current normalized wander direction
        public Random Rng;             // per-cat RNG for independent wander patterns
    }
}
