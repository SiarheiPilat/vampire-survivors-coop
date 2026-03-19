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

                // Fire in the player's facing direction
                float2 dir2   = math.normalizesafe(facing.ValueRO.Value);
                float3 dir    = new float3(dir2.x, dir2.y, 0f);
                float  damage = weapon.ValueRO.Damage * stats.ValueRO.Might;

                var proj = ecb.CreateEntity();
                ecb.AddComponent(proj, new Projectile
                {
                    Damage     = damage,
                    Speed      = weapon.ValueRO.Speed,
                    Direction  = dir,
                    MaxRange   = weapon.ValueRO.MaxRange,
                    Traveled   = 0f,
                    BounceCount = weapon.ValueRO.Bounces
                });
                ecb.AddComponent(proj, LocalTransform.FromPosition(transform.ValueRO.Position));
            }
        }
    }
}
