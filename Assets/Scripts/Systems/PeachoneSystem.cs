using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Peachone — fires Amount egg projectiles centered on a rotating Angle each Cooldown seconds.
    /// The angle advances +30° clockwise every cycle, creating a spiralling pattern.
    /// Wiki base stats: Damage 10, Cooldown 1.4 s, Speed 6 u/s, MaxRange 5 u, Amount 1.
    ///
    /// Evolved (Vandalier = Peachone + Ebony Wings):
    ///   15 dmg, 0.7s CD; fires from BOTH Angle and Angle+180° each cycle.
    ///   EbonyWingsSystem goes silent when IsEvolved.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct PeachoneSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            if (!SystemAPI.HasSingleton<BulletPrefabData>()) return;
            var bulletPrefab = SystemAPI.GetSingleton<BulletPrefabData>().BulletPrefab;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (pe, transform, stats) in
                SystemAPI.Query<RefRW<PeachoneState>, RefRO<LocalTransform>, RefRO<PlayerStats>>()
                    .WithAll<PlayerTag>()
                    .WithNone<Downed>())
            {
                pe.ValueRW.Timer -= dt;
                if (pe.ValueRO.Timer > 0f) continue;

                pe.ValueRW.Timer = pe.ValueRO.Cooldown * stats.ValueRO.CooldownMult;

                float  dmg    = pe.ValueRO.Damage * stats.ValueRO.Might;
                float  spd    = pe.ValueRO.Speed  * stats.ValueRO.ProjectileSpeedMult;
                int    amount = math.max(1, pe.ValueRO.Amount);
                float  range  = pe.ValueRO.MaxRange;
                float3 origin = transform.ValueRO.Position;
                float  angle  = pe.ValueRO.Angle;

                // Fire CW volley
                SpawnVolley(ref ecb, bulletPrefab, origin, angle, amount, dmg, spd, range);

                // When evolved (Vandalier): also fire CCW volley at angle+π
                if (pe.ValueRO.IsEvolved)
                    SpawnVolley(ref ecb, bulletPrefab, origin, angle + math.PI, amount, dmg, spd, range);

                // Advance angle +30° clockwise
                pe.ValueRW.Angle = math.fmod(angle + math.PI / 6f, math.PI * 2f);
            }
        }

        static void SpawnVolley(ref EntityCommandBuffer ecb, Entity prefab,
                                float3 origin, float centerAngle,
                                int amount, float dmg, float spd, float range)
        {
            for (int a = 0; a < amount; a++)
            {
                float spreadRad = amount > 1
                    ? math.radians((a - (amount - 1) * 0.5f) * 8f)
                    : 0f;
                float  totalAngle = centerAngle + spreadRad;
                float2 dir2       = new float2(math.cos(totalAngle), math.sin(totalAngle));

                var bullet = ecb.Instantiate(prefab);
                ecb.AddComponent(bullet, new Projectile
                {
                    Damage    = dmg,
                    Speed     = spd,
                    Direction = new float3(dir2.x, dir2.y, 0f),
                    MaxRange  = range,
                    Traveled  = 0f,
                });
                ecb.SetComponent(bullet, LocalTransform.FromPositionRotationScale(
                    origin, quaternion.identity, 0.2f));
            }
        }
    }
}
