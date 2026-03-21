using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Moves HealthPickup entities toward the nearest player when within MagnetRadius
    /// (4u base × MagnetRadiusMult; scales with Attractorb passive).
    /// Collects when within CollectRadius (0.6u): restores HealAmount HP capped at MaxHp.
    /// </summary>
    [BurstCompile]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct HealthPickupSystem : ISystem
    {
        ComponentLookup<Health> _healthLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _healthLookup = state.GetComponentLookup<Health>(isReadOnly: false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _healthLookup.Update(ref state);

            var playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag, LocalTransform, Health, PlayerStats>()
                .WithNone<Downed>()
                .Build();

            if (playerQuery.IsEmpty) return;

            var playerEntities   = playerQuery.ToEntityArray(Allocator.TempJob);
            var playerTransforms = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            var playerStats      = playerQuery.ToComponentDataArray<PlayerStats>(Allocator.TempJob);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            new CollectHealthJob
            {
                PlayerEntities      = playerEntities,
                PlayerTransforms    = playerTransforms,
                PlayerMagnetMults   = ExtractMagnetMults(playerStats, Allocator.TempJob),
                HealthLookup        = _healthLookup,
                Ecb                 = ecb,
                DeltaTime           = SystemAPI.Time.DeltaTime,
            }.Run();

            playerEntities.Dispose();
            playerTransforms.Dispose();
            playerStats.Dispose();
        }

        static NativeArray<float> ExtractMagnetMults(NativeArray<PlayerStats> stats, Allocator alloc)
        {
            var result = new NativeArray<float>(stats.Length, alloc);
            for (int i = 0; i < stats.Length; i++)
                result[i] = math.max(1f, stats[i].MagnetRadiusMult);
            return result;
        }

        [BurstCompile]
        partial struct CollectHealthJob : IJobEntity
        {
            const float BaseMagnetRadius = 4f;   // scales with Attractorb
            const float CollectRadius    = 0.6f;
            const float PickupSpeed      = 6f;

            [ReadOnly] public NativeArray<Entity>         PlayerEntities;
            [ReadOnly] public NativeArray<LocalTransform> PlayerTransforms;
            [ReadOnly] public NativeArray<float>          PlayerMagnetMults;
            [NativeDisableParallelForRestriction] public ComponentLookup<Health> HealthLookup;
            public EntityCommandBuffer Ecb;
            public float               DeltaTime;

            void Execute(Entity entity, ref LocalTransform transform, in HealthPickup pickup)
            {
                // Find nearest player within magnet radius
                int   nearestIdx  = -1;
                float nearestDist = float.MaxValue;

                for (int i = 0; i < PlayerEntities.Length; i++)
                {
                    float dist         = math.distance(transform.Position.xy, PlayerTransforms[i].Position.xy);
                    float magnetRadius = BaseMagnetRadius * PlayerMagnetMults[i];
                    if (dist < magnetRadius && dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestIdx  = i;
                    }
                }

                if (nearestIdx < 0) return;

                if (nearestDist <= CollectRadius)
                {
                    var hp     = HealthLookup[PlayerEntities[nearestIdx]];
                    hp.Current = math.min(hp.Current + pickup.HealAmount, hp.Max);
                    HealthLookup[PlayerEntities[nearestIdx]] = hp;
                    Ecb.DestroyEntity(entity);
                }
                else
                {
                    // Move toward player
                    float3 dir = math.normalizesafe(PlayerTransforms[nearestIdx].Position - transform.Position);
                    transform.Position += new float3(dir.x, dir.y, 0f) * PickupSpeed * DeltaTime;
                }
            }
        }
    }
}
