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

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (wand, transform) in
                SystemAPI.Query<RefRW<MagicWandState>, RefRO<LocalTransform>>()
                    .WithAll<PlayerTag>()
                    .WithNone<Downed>())
            {
                wand.ValueRW.Timer -= dt;
                if (wand.ValueRO.Timer > 0f) continue;

                wand.ValueRW.Timer = wand.ValueRO.Cooldown;

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

                float3 dir = math.normalizesafe(
                    enemyTransforms[nearestIdx].Position - transform.ValueRO.Position);

                var bullet = ecb.CreateEntity();
                ecb.AddComponent(bullet, new Projectile
                {
                    Damage    = wand.ValueRO.Damage,
                    Speed     = wand.ValueRO.Speed,
                    Direction = dir,
                    MaxRange  = wand.ValueRO.MaxRange,
                    Traveled  = 0f
                });
                ecb.AddComponent(bullet, LocalTransform.FromPosition(transform.ValueRO.Position));
            }

            enemyTransforms.Dispose();
        }
    }
}
