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
        EntityQuery _playerQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag, LocalTransform>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_playerQuery.IsEmpty) return;

            var playerPositions = _playerQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

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

            void Execute(in EnemyStats stats, ref LocalTransform transform, ref Knockback knockback)
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

                float3 dir      = math.normalizesafe(nearest - transform.Position);
                float3 movement = dir * stats.MoveSpeed * DeltaTime;
                transform.Position += new float3(movement.x, movement.y, 0f);

                // Apply knockback impulse and decay it (~0.25 s to dissipate)
                if (math.lengthsq(knockback.Velocity) > 0.01f)
                {
                    transform.Position += new float3(knockback.Velocity.x, knockback.Velocity.y, 0f) * DeltaTime;
                    knockback.Velocity *= math.max(0f, 1f - 12f * DeltaTime);
                }
                else
                {
                    knockback.Velocity = float2.zero;
                }
            }
        }
    }
}
