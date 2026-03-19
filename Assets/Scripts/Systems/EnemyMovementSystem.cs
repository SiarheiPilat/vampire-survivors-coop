using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Moves each enemy toward the nearest player.
    /// Burst-compiled. O(enemies × players) — fine for current scale.
    /// </summary>
    [BurstCompile]
    public partial struct EnemyMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag, LocalTransform>()
                .Build();

            if (playerQuery.IsEmpty) return;

            var playerPositions = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            var job = new MoveTowardPlayerJob
            {
                PlayerPositions = playerPositions,
                DeltaTime       = SystemAPI.Time.DeltaTime
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
            playerPositions.Dispose(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(EnemyTag))]
        partial struct MoveTowardPlayerJob : IJobEntity
        {
            [ReadOnly] public NativeArray<LocalTransform> PlayerPositions;
            public float DeltaTime;

            void Execute(in EnemyStats stats, ref LocalTransform transform)
            {
                float3 nearest   = PlayerPositions[0].Position;
                float  minDistSq = math.distancesq(transform.Position, nearest);

                for (int i = 1; i < PlayerPositions.Length; i++)
                {
                    float d = math.distancesq(transform.Position, PlayerPositions[i].Position);
                    if (d < minDistSq)
                    {
                        minDistSq = d;
                        nearest   = PlayerPositions[i].Position;
                    }
                }

                float3 dir = math.normalizesafe(nearest - transform.Position);
                transform.Position += dir * stats.MoveSpeed * DeltaTime;
            }
        }
    }
}
