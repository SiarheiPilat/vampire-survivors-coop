using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Ebony Wings — fires Amount bat projectiles centered on a rotating Angle each Cooldown seconds.
    /// The angle advances -30° counterclockwise every cycle, paired with Peachone's CW rotation.
    /// Wiki base stats: Damage 10, Cooldown 1.4 s, Speed 6 u/s, MaxRange 5 u, Amount 1.
    ///
    /// Evolved (Vandalier): IsEvolved=true; this system goes silent.
    /// PeachoneSystem handles all shots when evolved (fires both CW and CCW).
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct EbonyWingsSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            if (!SystemAPI.HasSingleton<BulletPrefabData>()) return;
            var bulletPrefab = SystemAPI.GetSingleton<BulletPrefabData>().BulletPrefab;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (ew, transform, stats) in
                SystemAPI.Query<RefRW<EbonyWingsState>, RefRO<LocalTransform>, RefRO<PlayerStats>>()
                    .WithAll<PlayerTag>()
                    .WithNone<Downed>())
            {
                // Evolved → Peachone handles all shots; just tick the timer so Angle stays valid
                if (ew.ValueRO.IsEvolved)
                {
                    ew.ValueRW.Timer -= dt;
                    if (ew.ValueRO.Timer <= 0f)
                    {
                        ew.ValueRW.Timer = ew.ValueRO.Cooldown * stats.ValueRO.CooldownMult;
                        ew.ValueRW.Angle = math.fmod(ew.ValueRO.Angle - math.PI / 6f + math.PI * 2f, math.PI * 2f);
                    }
                    continue;
                }

                ew.ValueRW.Timer -= dt;
                if (ew.ValueRO.Timer > 0f) continue;

                ew.ValueRW.Timer = ew.ValueRO.Cooldown * stats.ValueRO.CooldownMult;

                float  dmg    = ew.ValueRO.Damage * stats.ValueRO.Might;
                float  spd    = ew.ValueRO.Speed  * stats.ValueRO.ProjectileSpeedMult;
                int    amount = math.max(1, ew.ValueRO.Amount);
                float  range  = ew.ValueRO.MaxRange;
                float3 origin = transform.ValueRO.Position;
                float  angle  = ew.ValueRO.Angle;

                for (int a = 0; a < amount; a++)
                {
                    float spreadRad = amount > 1
                        ? math.radians((a - (amount - 1) * 0.5f) * 8f)
                        : 0f;
                    float  totalAngle = angle + spreadRad;
                    float2 dir2       = new float2(math.cos(totalAngle), math.sin(totalAngle));

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
                        origin, quaternion.identity, 0.2f));
                }

                // Advance angle -30° counterclockwise
                ew.ValueRW.Angle = math.fmod(angle - math.PI / 6f + math.PI * 2f, math.PI * 2f);
            }
        }
    }
}
