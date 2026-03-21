using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Phiera Der Tuphello — fires Amount bullets simultaneously in each of the
    /// four cardinal directions (right/up/left/down) every Cooldown seconds.
    /// Each firing cycle spawns Amount×4 projectiles.
    /// Wiki base stats: Damage 5, Cooldown 1.4 s, Speed ~12 u/s, Amount 1 (per direction).
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct PhieraSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            if (!SystemAPI.HasSingleton<BulletPrefabData>()) return;
            var bulletPrefab = SystemAPI.GetSingleton<BulletPrefabData>().BulletPrefab;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (ph, transform, stats) in
                SystemAPI.Query<RefRW<PhieraState>, RefRO<LocalTransform>, RefRO<PlayerStats>>()
                    .WithAll<PlayerTag>()
                    .WithNone<Downed>())
            {
                ph.ValueRW.Timer -= dt;
                if (ph.ValueRO.Timer > 0f) continue;

                ph.ValueRW.Timer = ph.ValueRO.Cooldown * stats.ValueRO.CooldownMult;

                float  dmg    = ph.ValueRO.Damage * stats.ValueRO.Might;
                float  spd    = ph.ValueRO.Speed  * stats.ValueRO.ProjectileSpeedMult;
                int    amount = math.max(1, ph.ValueRO.Amount);
                float  range  = ph.ValueRO.MaxRange;
                float3 origin = transform.ValueRO.Position;

                // Four cardinal base directions (right, up, left, down)
                for (int d = 0; d < 4; d++)
                {
                    float baseAngle = d * math.PI * 0.5f; // 0°, 90°, 180°, 270°
                    float baseCos   = math.cos(baseAngle);
                    float baseSin   = math.sin(baseAngle);

                    for (int a = 0; a < amount; a++)
                    {
                        // Spread extra bullets with ±8° per slot
                        float spreadRad = amount > 1
                            ? math.radians((a - (amount - 1) * 0.5f) * 8f)
                            : 0f;
                        float totalAngle = baseAngle + spreadRad;
                        float2 dir2 = new float2(math.cos(totalAngle), math.sin(totalAngle));

                        var bullet = ecb.Instantiate(bulletPrefab);
                        ecb.AddComponent(bullet, new Projectile
                        {
                            Damage    = dmg,
                            Speed     = spd,
                            Direction = new float3(dir2.x, dir2.y, 0f),
                            MaxRange  = range,
                            Traveled  = 0f,
                        });
                        ecb.SetComponent(bullet, LocalTransform.FromPositionRotationScale(
                            origin, quaternion.identity, 0.18f));
                    }
                }
            }
        }
    }
}
