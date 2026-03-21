using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Eight The Sparrow — Pugnala's blue pistol, paired with Phiera Der Tuphello.
    /// Fires Amount bullets simultaneously in each of the four diagonal directions
    /// (NE / NW / SW / SE — 45°, 135°, 225°, 315°) every Cooldown seconds.
    /// When IsEvolved (Phieraggi), goes silent — PhieraSystem fires all 8 directions instead.
    /// Wiki base stats: Damage 5, Cooldown 1.4 s, Speed ~12 u/s, Amount 1 (per direction).
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(PhieraSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct EightSparrowSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            if (!SystemAPI.HasSingleton<BulletPrefabData>()) return;
            var bulletPrefab = SystemAPI.GetSingleton<BulletPrefabData>().BulletPrefab;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (eight, transform, stats) in
                SystemAPI.Query<RefRW<EightSparrowState>, RefRO<LocalTransform>, RefRO<PlayerStats>>()
                    .WithAll<PlayerTag>()
                    .WithNone<Downed>())
            {
                // Phieraggi evolution: Eight goes silent, Phiera handles all 8 directions
                if (eight.ValueRO.IsEvolved) continue;

                eight.ValueRW.Timer -= dt;
                if (eight.ValueRO.Timer > 0f) continue;

                eight.ValueRW.Timer = eight.ValueRO.Cooldown * stats.ValueRO.CooldownMult;

                float  dmg    = eight.ValueRO.Damage * stats.ValueRO.Might;
                float  spd    = eight.ValueRO.Speed  * stats.ValueRO.ProjectileSpeedMult;
                int    amount = math.max(1, eight.ValueRO.Amount);
                float  range  = eight.ValueRO.MaxRange;
                float3 origin = transform.ValueRO.Position;

                // Four diagonal base directions (NE, NW, SW, SE = 45°, 135°, 225°, 315°)
                for (int d = 0; d < 4; d++)
                {
                    float baseAngle = math.PI * 0.25f + d * math.PI * 0.5f; // 45°, 135°, 225°, 315°

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
