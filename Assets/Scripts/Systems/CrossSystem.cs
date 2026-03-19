using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Fires a returning cross projectile every Cooldown seconds.
    /// The cross travels in the player's facing direction up to TurnDistance, then
    /// reverses and homes back to the player (handled by ProjectileMovementSystem).
    /// ProjectileHitSystem destroys it on the first enemy hit.
    ///
    /// Wiki base stats: Damage 50, Cooldown 5.0 s, Speed 15 u/s.
    /// TurnDistance 8 u — outward arc takes ~0.5 s at base speed.
    /// MaxRange 30 u — safety despawn if owner is unreachable.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct CrossSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            if (!SystemAPI.HasSingleton<BulletPrefabData>()) return;
            var bulletPrefab = SystemAPI.GetSingleton<BulletPrefabData>().BulletPrefab;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (cross, facing, transform, stats, entity) in
                SystemAPI.Query<RefRW<CrossState>, RefRO<FacingDirection>, RefRO<LocalTransform>, RefRO<PlayerStats>>()
                    .WithAll<PlayerTag>()
                    .WithNone<Downed>()
                    .WithEntityAccess())
            {
                cross.ValueRW.Timer -= dt;
                if (cross.ValueRO.Timer > 0f) continue;

                cross.ValueRW.Timer = cross.ValueRO.Cooldown;

                float2 dir2 = math.lengthsq(facing.ValueRO.Value) > 0.001f
                    ? math.normalize(facing.ValueRO.Value)
                    : new float2(1f, 0f);

                var bullet = ecb.Instantiate(bulletPrefab);
                ecb.AddComponent(bullet, new Projectile
                {
                    Damage       = cross.ValueRO.Damage * stats.ValueRO.Might,
                    Speed        = cross.ValueRO.Speed,
                    Direction    = new float3(dir2.x, dir2.y, 0f),
                    MaxRange     = 30f,  // safety despawn if owner is gone
                    Traveled     = 0f,
                    TurnDistance = cross.ValueRO.TurnDistance,
                    OwnerEntity  = entity
                });
                ecb.SetComponent(bullet, LocalTransform.FromPositionRotationScale(
                    transform.ValueRO.Position, quaternion.identity, 0.25f));
            }
        }
    }
}
