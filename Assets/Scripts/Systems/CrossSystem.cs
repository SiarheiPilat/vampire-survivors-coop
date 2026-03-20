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

                cross.ValueRW.Timer = cross.ValueRO.Cooldown * stats.ValueRO.CooldownMult;

                float2 baseDir = math.lengthsq(facing.ValueRO.Value) > 0.001f
                    ? math.normalize(facing.ValueRO.Value)
                    : new float2(1f, 0f);

                float crossSpd = cross.ValueRO.Speed * stats.ValueRO.ProjectileSpeedMult;

                if (cross.ValueRO.IsEvolved)
                {
                    // Heaven Sword: fire Count (2) piercing swords at ±15° from facing, no return
                    int   count  = cross.ValueRO.Count > 0 ? cross.ValueRO.Count : 2;
                    float spread = math.PI / 12f; // 15 degrees
                    float startAngle = count > 1 ? -spread * (count - 1) * 0.5f : 0f;

                    for (int s = 0; s < count; s++)
                    {
                        float  angle  = startAngle + spread * s;
                        float  cos    = math.cos(angle);
                        float  sin    = math.sin(angle);
                        float2 dir2   = new float2(
                            baseDir.x * cos - baseDir.y * sin,
                            baseDir.x * sin + baseDir.y * cos);

                        var sword = ecb.Instantiate(bulletPrefab);
                        ecb.AddComponent(sword, new Projectile
                        {
                            Damage        = cross.ValueRO.Damage * stats.ValueRO.Might,
                            Speed         = crossSpd,
                            Direction     = new float3(dir2.x, dir2.y, 0f),
                            MaxRange      = 20f,
                            Traveled      = 0f,
                            TurnDistance  = 0f,   // no return
                            Piercing      = true,
                            LastPierceHit = Entity.Null,
                        });
                        ecb.SetComponent(sword, LocalTransform.FromPositionRotationScale(
                            transform.ValueRO.Position, quaternion.identity, 0.3f));
                    }
                }
                else
                {
                    var bullet = ecb.Instantiate(bulletPrefab);
                    ecb.AddComponent(bullet, new Projectile
                    {
                        Damage       = cross.ValueRO.Damage * stats.ValueRO.Might,
                        Speed        = crossSpd,
                        Direction    = new float3(baseDir.x, baseDir.y, 0f),
                        MaxRange     = 30f,
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
}
