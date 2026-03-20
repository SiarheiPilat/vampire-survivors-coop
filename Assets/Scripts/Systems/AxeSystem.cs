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

                axe.ValueRW.Timer = axe.ValueRO.Cooldown * stats.ValueRO.CooldownMult;

                float facingX = math.lengthsq(facing.ValueRO.Value) > 0.001f
                    ? math.normalize(facing.ValueRO.Value).x
                    : 1f;

                float spd    = axe.ValueRO.Speed * stats.ValueRO.ProjectileSpeedMult;
                float damage = axe.ValueRO.Damage * stats.ValueRO.Might;
                int   amount = math.max(1, axe.ValueRO.Amount);

                if (axe.ValueRO.IsEvolved)
                {
                    // ── Death Spiral: 9 piercing scythes fanned evenly around 360° ──
                    // Wiki stats: 60 dmg, 4.0s CD (already applied above), speed 0.8, pierce
                    const int   ScytheCount = 9;
                    const float SpiralSpeed = 0.8f;
                    const float SpiralRange = 20f; // slow + long range = stays on screen
                    float  spiralDmg     = damage; // Might already applied
                    float  spiralSpd     = SpiralSpeed * stats.ValueRO.ProjectileSpeedMult;
                    float  angleStep     = (math.PI * 2f) / ScytheCount;

                    for (int a = 0; a < ScytheCount; a++)
                    {
                        float  angle = a * angleStep;
                        float3 dir   = new float3(math.cos(angle), math.sin(angle), 0f);
                        var bullet = ecb.Instantiate(bulletPrefab);
                        ecb.AddComponent(bullet, new Projectile
                        {
                            Damage    = spiralDmg,
                            Speed     = spiralSpd,
                            Direction = dir,
                            MaxRange  = SpiralRange,
                            Traveled  = 0f,
                            Gravity   = 0f,
                            Piercing  = true,
                        });
                        ecb.SetComponent(bullet, LocalTransform.FromPositionRotationScale(
                            transform.ValueRO.Position, quaternion.identity, 0.35f));
                    }
                }
                else
                {
                    // ── Normal Axe: parabolic arc fan ──────────────────────────────
                    float2 baseDir2  = math.normalizesafe(new float2(facingX * 0.5f, 0.866f));
                    float  stepRad   = 25f * math.PI / 180f;
                    float  centreOff = -(amount - 1) * 0.5f * stepRad;

                    for (int a = 0; a < amount; a++)
                    {
                        float  offset = centreOff + a * stepRad;
                        float  cosO   = math.cos(offset);
                        float  sinO   = math.sin(offset);
                        float2 fanDir = new float2(
                            baseDir2.x * cosO - baseDir2.y * sinO,
                            baseDir2.x * sinO + baseDir2.y * cosO);
                        var initVel = new float3(fanDir.x * spd, fanDir.y * spd, 0f);

                        var bullet = ecb.Instantiate(bulletPrefab);
                        ecb.AddComponent(bullet, new Projectile
                        {
                            Damage    = damage,
                            Speed     = spd,
                            Direction = math.normalizesafe(initVel),
                            MaxRange  = axe.ValueRO.MaxRange,
                            Traveled  = 0f,
                            Gravity   = axe.ValueRO.Gravity,
                            Velocity  = initVel
                        });
                        ecb.SetComponent(bullet, LocalTransform.FromPositionRotationScale(
                            transform.ValueRO.Position, quaternion.identity, 0.3f));
                    }
                }
            }
        }
    }
}
