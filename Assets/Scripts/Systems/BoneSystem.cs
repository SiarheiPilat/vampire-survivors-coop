using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Fires bouncing Bone projectiles in the player's facing direction.
    /// Bones reflect off virtual walls (axis-aligned bounce when MaxRange is
    /// exceeded per segment) up to BounceCount times — same mechanic as Runetracer.
    ///
    /// When Amount > 1 additional bones fan out at ±20° per step.
    ///
    /// Wiki base stats: Damage 30, Speed 8 u/s, Cooldown 0.5 s, Bounces 2, MaxRange 12 u.
    /// Mortaccio's starter weapon. Higher damage and fewer bounces than Runetracer.
    /// </summary>
    [BurstCompile]
    [UpdateBefore(typeof(ProjectileMovementSystem))]
    public partial struct BoneSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (weapon, stats, transform, facing) in
                SystemAPI.Query<RefRW<BoneState>, RefRO<PlayerStats>, RefRO<LocalTransform>, RefRO<FacingDirection>>()
                    .WithAll<PlayerTag>().WithNone<Downed>())
            {
                weapon.ValueRW.Timer -= dt;
                if (weapon.ValueRO.Timer > 0f) continue;

                weapon.ValueRW.Timer = weapon.ValueRO.Cooldown * stats.ValueRO.CooldownMult;

                float2 baseDir2 = math.normalizesafe(facing.ValueRO.Value);
                if (math.lengthsq(baseDir2) < 0.001f) baseDir2 = new float2(1f, 0f);

                float damage = weapon.ValueRO.Damage * stats.ValueRO.Might;
                float spd    = weapon.ValueRO.Speed  * stats.ValueRO.ProjectileSpeedMult;
                int   amount = math.max(1, weapon.ValueRO.Amount);

                // Fan spread: 20° between bones, centred on facing direction
                float baseAngle  = math.atan2(baseDir2.y, baseDir2.x);
                float stepRad    = 20f * math.PI / 180f;
                float centreOff  = -(amount - 1) * 0.5f * stepRad;

                for (int a = 0; a < amount; a++)
                {
                    float  angle = baseAngle + centreOff + a * stepRad;
                    float3 dir   = new float3(math.cos(angle), math.sin(angle), 0f);

                    var proj = ecb.CreateEntity();
                    ecb.AddComponent(proj, new Projectile
                    {
                        Damage      = damage,
                        Speed       = spd,
                        Direction   = dir,
                        MaxRange    = weapon.ValueRO.MaxRange,
                        Traveled    = 0f,
                        BounceCount = weapon.ValueRO.Bounces
                    });
                    ecb.AddComponent(proj, LocalTransform.FromPosition(transform.ValueRO.Position));
                }
            }
        }
    }
}
