using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Fires a Magic Wand projectile from each non-downed player once per cooldown.
    /// Targets the nearest living enemy; projectile travels in a straight line.
    /// Wiki base stats: Damage 10, Speed 10 u/s, Cooldown 0.5 s, Range 15 u.
    /// Runs single-threaded to avoid write races on MagicWandState.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct MagicWandSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            var enemyQuery = SystemAPI.QueryBuilder()
                .WithAll<EnemyTag, LocalTransform>()
                .Build();

            if (enemyQuery.IsEmpty) return;

            var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            if (!SystemAPI.HasSingleton<BulletPrefabData>()) return;
            var bulletPrefab = SystemAPI.GetSingleton<BulletPrefabData>().BulletPrefab;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (wand, transform, stats) in
                SystemAPI.Query<RefRW<MagicWandState>, RefRO<LocalTransform>, RefRO<PlayerStats>>()
                    .WithAll<PlayerTag>()
                    .WithNone<Downed>())
            {
                wand.ValueRW.Timer -= dt;
                if (wand.ValueRO.Timer > 0f) continue;

                wand.ValueRW.Timer = wand.ValueRO.Cooldown * stats.ValueRO.CooldownMult;

                // Find nearest enemy
                int   nearestIdx  = -1;
                float nearestDist = float.MaxValue;
                for (int i = 0; i < enemyTransforms.Length; i++)
                {
                    float dist = math.distance(
                        transform.ValueRO.Position.xy,
                        enemyTransforms[i].Position.xy);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestIdx  = i;
                    }
                }

                if (nearestIdx < 0) continue;

                float3 primaryDir = math.normalizesafe(
                    enemyTransforms[nearestIdx].Position - transform.ValueRO.Position);

                // Fan spread: 20° (base) or 10° (Holy Wand evolved), centered on primary direction
                float baseAngle  = math.atan2(primaryDir.y, primaryDir.x);
                float spreadRad  = wand.ValueRO.IsEvolved ? math.radians(10f) : math.radians(20f);
                int   amount     = math.max(1, wand.ValueRO.Amount);
                float halfSpread  = (amount - 1) * 0.5f * spreadRad;

                float dmg = wand.ValueRO.Damage * stats.ValueRO.Might;
                for (int s = 0; s < amount; s++)
                {
                    float a      = baseAngle - halfSpread + s * spreadRad;
                    float3 dir   = new float3(math.cos(a), math.sin(a), 0f);
                    var bullet   = ecb.Instantiate(bulletPrefab);
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

            enemyTransforms.Dispose();
        }
    }
}
