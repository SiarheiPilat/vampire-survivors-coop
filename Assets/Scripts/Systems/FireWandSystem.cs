using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Fires a fireball in a random direction every Cooldown seconds.
    /// Unlike MagicWandSystem (aims at nearest enemy), Fire Wand fires randomly,
    /// making it crowd-clearing but unpredictable at range.
    /// Uses a per-player Unity.Mathematics.Random stored in FireWandState so
    /// multiple players fire with independent RNG streams.
    /// Wiki base stats: Damage 10, Speed 11 u/s, Cooldown 0.4 s.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct FireWandSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            if (!SystemAPI.HasSingleton<BulletPrefabData>()) return;
            var bulletPrefab = SystemAPI.GetSingleton<BulletPrefabData>().BulletPrefab;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (wand, transform, stats) in
                SystemAPI.Query<RefRW<FireWandState>, RefRO<LocalTransform>, RefRO<PlayerStats>>()
                    .WithAll<PlayerTag>()
                    .WithNone<Downed>())
            {
                wand.ValueRW.Timer -= dt;
                if (wand.ValueRO.Timer > 0f) continue;

                wand.ValueRW.Timer = wand.ValueRO.Cooldown * stats.ValueRO.CooldownMult;

                float dmg    = wand.ValueRO.Damage * stats.ValueRO.Might;
                int   amount = math.max(1, wand.ValueRO.Amount);

                // Each fireball fires in an independent random direction
                for (int s = 0; s < amount; s++)
                {
                    float  angle  = wand.ValueRW.Rng.NextFloat(0f, 2f * math.PI);
                    float3 dir    = new float3(math.cos(angle), math.sin(angle), 0f);
                    var    bullet = ecb.Instantiate(bulletPrefab);
                    ecb.AddComponent(bullet, new Projectile
                    {
                        Damage    = dmg,
                        Speed     = wand.ValueRO.Speed,
                        Direction = dir,
                        MaxRange  = wand.ValueRO.MaxRange,
                        Traveled  = 0f
                    });
                    ecb.SetComponent(bullet, LocalTransform.FromPositionRotationScale(
                        transform.ValueRO.Position, quaternion.identity, 0.2f));
                }
            }
        }
    }
}
