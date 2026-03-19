using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Fires a knife projectile in the player's facing direction every Cooldown seconds.
    /// Unlike Magic Wand (nearest-enemy aim), the knife travels in the last movement direction,
    /// defaulting to right on spawn. Projectile is handled by the shared Projectile system.
    /// Wiki base stats: Damage 10, Speed 15 u/s, Cooldown 0.35 s.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct KnifeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            if (!SystemAPI.HasSingleton<BulletPrefabData>()) return;
            var bulletPrefab = SystemAPI.GetSingleton<BulletPrefabData>().BulletPrefab;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (knife, facing, transform, stats) in
                SystemAPI.Query<RefRW<KnifeState>, RefRO<FacingDirection>, RefRO<LocalTransform>, RefRO<PlayerStats>>()
                    .WithAll<PlayerTag>()
                    .WithNone<Downed>())
            {
                knife.ValueRW.Timer -= dt;
                if (knife.ValueRO.Timer > 0f) continue;

                knife.ValueRW.Timer = knife.ValueRO.Cooldown * stats.ValueRO.CooldownMult;

                float2 dir2 = math.lengthsq(facing.ValueRO.Value) > 0.001f
                    ? math.normalize(facing.ValueRO.Value)
                    : new float2(1f, 0f); // default right

                // Fan spread: 20° between blades, centered on facing direction
                float baseAngle  = math.atan2(dir2.y, dir2.x);
                float spreadRad  = math.radians(20f);
                int   amount     = math.max(1, knife.ValueRO.Amount);
                float halfSpread = (amount - 1) * 0.5f * spreadRad;
                float dmg        = knife.ValueRO.Damage * stats.ValueRO.Might;

                for (int s = 0; s < amount; s++)
                {
                    float  a   = baseAngle - halfSpread + s * spreadRad;
                    float3 dir = new float3(math.cos(a), math.sin(a), 0f);
                    var bullet  = ecb.Instantiate(bulletPrefab);
                    ecb.AddComponent(bullet, new Projectile
                    {
                        Damage    = dmg,
                        Speed     = knife.ValueRO.Speed,
                        Direction = dir,
                        MaxRange  = knife.ValueRO.MaxRange,
                        Traveled  = 0f
                    });
                    ecb.SetComponent(bullet, LocalTransform.FromPositionRotationScale(
                        transform.ValueRO.Position, quaternion.identity, 0.2f));
                }
            }
        }
    }
}
