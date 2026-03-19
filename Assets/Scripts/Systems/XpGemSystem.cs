using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Moves XpGem entities toward the nearest player within MagnetRadius (30 units).
    /// When a gem reaches CollectRadius (0.5 units) of a player, it is absorbed:
    /// XP is added to the player's PlayerStats.Xp and the gem entity is destroyed.
    /// Runs single-threaded to avoid write races on shared PlayerStats.
    /// </summary>
    [BurstCompile]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct XpGemSystem : ISystem
    {
        ComponentLookup<PlayerStats> _statsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _statsLookup = state.GetComponentLookup<PlayerStats>(isReadOnly: false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _statsLookup.Update(ref state);

            var playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag, LocalTransform, PlayerStats>()
                .Build();

            if (playerQuery.IsEmpty) return;

            var playerEntities   = playerQuery.ToEntityArray(Allocator.TempJob);
            var playerTransforms = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            new CollectGemJob
            {
                PlayerEntities   = playerEntities,
                PlayerTransforms = playerTransforms,
                StatsLookup      = _statsLookup,
                Ecb              = ecb,
                DeltaTime        = SystemAPI.Time.DeltaTime
            }.Run();

            playerEntities.Dispose();
            playerTransforms.Dispose();
        }

        [BurstCompile]
        partial struct CollectGemJob : IJobEntity
        {
            const float MagnetRadius  = 30f;
            const float CollectRadius = 0.5f;
            const float GemSpeed      = 8f;

            [ReadOnly] public NativeArray<Entity>         PlayerEntities;
            [ReadOnly] public NativeArray<LocalTransform> PlayerTransforms;

            [NativeDisableParallelForRestriction] public ComponentLookup<PlayerStats> StatsLookup;

            public EntityCommandBuffer Ecb;
            public float DeltaTime;

            void Execute(Entity entity, ref LocalTransform transform, in XpGem gem)
            {
                // Find nearest player within MagnetRadius
                int   nearestIdx  = -1;
                float nearestDist = float.MaxValue;

                for (int i = 0; i < PlayerEntities.Length; i++)
                {
                    float dist = math.distance(transform.Position.xy, PlayerTransforms[i].Position.xy);
                    if (dist < MagnetRadius && dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestIdx  = i;
                    }
                }

                if (nearestIdx < 0) return; // no player in range

                if (nearestDist <= CollectRadius)
                {
                    // Collect: add XP scaled by Crown multiplier, destroy gem
                    var stats = StatsLookup[PlayerEntities[nearestIdx]];
                    stats.Xp += gem.Value * stats.XpMult;
                    StatsLookup[PlayerEntities[nearestIdx]] = stats;
                    Ecb.DestroyEntity(entity);
                }
                else
                {
                    // Move toward player
                    float3 dir     = math.normalizesafe(PlayerTransforms[nearestIdx].Position - transform.Position);
                    float3 move    = dir * GemSpeed * DeltaTime;
                    transform.Position += new float3(move.x, move.y, 0f);
                }
            }
        }
    }
}
