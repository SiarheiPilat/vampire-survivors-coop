using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Fires a bouncing Runetracer projectile in the player's facing direction.
    /// The projectile reflects off virtual walls (axis-aligned bounce when MaxRange
    /// is exceeded) up to BounceCount times before expiring.
    ///
    /// Wiki base stats: Damage 10, Speed 8 u/s, Cooldown 0.35 s, Bounces 3, MaxRange 10 u.
    /// </summary>
    [BurstCompile]
    [UpdateBefore(typeof(ProjectileMovementSystem))]
    public partial struct RunetracerSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (weapon, stats, transform, facing, entity) in
                SystemAPI.Query<RefRW<RunetracerState>, RefRO<PlayerStats>, RefRO<LocalTransform>, RefRO<FacingDirection>>()
                    .WithAll<PlayerTag>().WithNone<Downed>().WithEntityAccess())
            {
                weapon.ValueRW.Timer -= dt;
                if (weapon.ValueRO.Timer > 0f) continue;

                weapon.ValueRW.Timer = weapon.ValueRO.Cooldown * stats.ValueRO.CooldownMult;

                // Fire in the player's facing direction (fan spread 20° between tracers if Amount > 1)
                float2 baseDir2  = math.normalizesafe(facing.ValueRO.Value);
                if (math.lengthsq(baseDir2) < 0.001f) baseDir2 = new float2(1f, 0f);
                float  damage    = weapon.ValueRO.Damage * stats.ValueRO.Might;
                float  spd       = weapon.ValueRO.Speed  * stats.ValueRO.ProjectileSpeedMult;
                int    amount    = math.max(1, weapon.ValueRO.Amount);

                float baseAngle = math.atan2(baseDir2.y, baseDir2.x);
                float stepRad   = 20f * math.PI / 180f;
                float centreOff = -(amount - 1) * 0.5f * stepRad;

                bool  explodes        = weapon.ValueRO.IsEvolved;
                float explosionRadius = explodes ? 1.5f : 0f;

                for (int a = 0; a < amount; a++)
                {
                    float  angle = baseAngle + centreOff + a * stepRad;
                    float3 dir   = new float3(math.cos(angle), math.sin(angle), 0f);

                    var proj = ecb.CreateEntity();
                    ecb.AddComponent(proj, new Projectile
                    {
                        Damage          = damage,
                        Speed           = spd,
                        Direction       = dir,
                        MaxRange        = weapon.ValueRO.MaxRange,
                        Traveled        = 0f,
                        BounceCount     = weapon.ValueRO.Bounces,
                        Explodes        = explodes,
                        ExplosionRadius = explosionRadius,
                    });
                    ecb.AddComponent(proj, LocalTransform.FromPosition(transform.ValueRO.Position));
                }
            }
        }
    }
}
