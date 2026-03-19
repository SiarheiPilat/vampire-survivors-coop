using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Fires an axe projectile in a parabolic arc every Cooldown seconds.
    /// The axe launches at ~60° upward in the player's facing direction,
    /// then curves back down via gravity — hitting enemies along the arc.
    ///
    /// Uses Projectile.Velocity + Projectile.Gravity so ProjectileMovementSystem
    /// handles integration; ProjectileHitSystem handles damage on contact.
    ///
    /// Launch: vx = facing * Speed * 0.5, vy = Speed * 0.866  (≈ 60° elevation)
    /// Gravity: 12 u/s² downward — full arc takes ~1.15 s, ~4.6 u horizontal.
    /// Wiki base stats: Damage 20, Cooldown 1.25 s.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct AxeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            if (!SystemAPI.HasSingleton<BulletPrefabData>()) return;
            var bulletPrefab = SystemAPI.GetSingleton<BulletPrefabData>().BulletPrefab;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (axe, facing, transform, stats) in
                SystemAPI.Query<RefRW<AxeState>, RefRO<FacingDirection>, RefRO<LocalTransform>, RefRO<PlayerStats>>()
                    .WithAll<PlayerTag>()
                    .WithNone<Downed>())
            {
                axe.ValueRW.Timer -= dt;
                if (axe.ValueRO.Timer > 0f) continue;

                axe.ValueRW.Timer = axe.ValueRO.Cooldown;

                // Horizontal component follows facing direction; always launches upward
                float facingX = math.lengthsq(facing.ValueRO.Value) > 0.001f
                    ? math.normalize(facing.ValueRO.Value).x
                    : 1f;

                float spd      = axe.ValueRO.Speed;
                var   initVel  = new float3(facingX * spd * 0.5f, spd * 0.866f, 0f); // ~60° elevation

                var bullet = ecb.Instantiate(bulletPrefab);
                ecb.AddComponent(bullet, new Projectile
                {
                    Damage    = axe.ValueRO.Damage * stats.ValueRO.Might,
                    Speed     = spd,
                    Direction = math.normalizesafe(initVel),
                    MaxRange  = axe.ValueRO.MaxRange,
                    Traveled  = 0f,
                    Gravity   = axe.ValueRO.Gravity,
                    Velocity  = initVel
                });
                ecb.SetComponent(bullet, LocalTransform.FromPositionRotationScale(
                    transform.ValueRO.Position, quaternion.identity, 0.3f)); // slightly larger than bullet
            }
        }
    }
}
