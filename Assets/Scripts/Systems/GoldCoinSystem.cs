using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Moves GoldCoin entities toward the nearest player when within MagnetRadius
    /// (4u base × MagnetRadiusMult; scales with Attractorb passive).
    /// Collects when within CollectRadius (0.6u): adds Value × GoldMult to SharedGold.
    /// </summary>
    [BurstCompile]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct GoldCoinSystem : ISystem
    {
        ComponentLookup<SharedGold> _sharedGoldLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _sharedGoldLookup = state.GetComponentLookup<SharedGold>(isReadOnly: false);
            state.RequireForUpdate<SharedGold>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _sharedGoldLookup.Update(ref state);

            var playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag, LocalTransform, PlayerStats>()
                .WithNone<Downed>()
                .Build();

            if (playerQuery.IsEmpty) return;

            var playerTransforms = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            var playerStats      = playerQuery.ToComponentDataArray<PlayerStats>(Allocator.TempJob);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            var goldAccum = new NativeReference<int>(0, Allocator.TempJob);

            new CollectCoinJob
            {
                PlayerTransforms    = playerTransforms,
                PlayerGoldMults     = ExtractGoldMults(playerStats, Allocator.TempJob),
                PlayerMagnetMults   = ExtractMagnetMults(playerStats, Allocator.TempJob),
                GoldAccum           = goldAccum,
                Ecb                 = ecb,
                DeltaTime           = SystemAPI.Time.DeltaTime,
            }.Run();

            playerTransforms.Dispose();
            playerStats.Dispose();

            // Apply accumulated gold to the singleton (main thread, after job completes)
            if (goldAccum.Value > 0)
            {
                var sgEntity = SystemAPI.GetSingletonEntity<SharedGold>();
                var sg       = _sharedGoldLookup[sgEntity];
                sg.Total    += goldAccum.Value;
                _sharedGoldLookup[sgEntity] = sg;
            }

            goldAccum.Dispose();
        }

        static NativeArray<float> ExtractGoldMults(NativeArray<PlayerStats> stats, Allocator alloc)
        {
            var result = new NativeArray<float>(stats.Length, alloc);
            for (int i = 0; i < stats.Length; i++)
                result[i] = math.max(1f, stats[i].GoldMult);
            return result;
        }

        static NativeArray<float> ExtractMagnetMults(NativeArray<PlayerStats> stats, Allocator alloc)
        {
            var result = new NativeArray<float>(stats.Length, alloc);
            for (int i = 0; i < stats.Length; i++)
                result[i] = math.max(1f, stats[i].MagnetRadiusMult);
            return result;
        }

        [BurstCompile]
        partial struct CollectCoinJob : IJobEntity
        {
            const float BaseMagnetRadius = 4f;   // scales with Attractorb
            const float CollectRadius    = 0.6f;
            const float CoinSpeed        = 6f;

            [ReadOnly] public NativeArray<LocalTransform> PlayerTransforms;
            [ReadOnly] public NativeArray<float>          PlayerGoldMults;
            [ReadOnly] public NativeArray<float>          PlayerMagnetMults;
            public NativeReference<int>                   GoldAccum;
            public EntityCommandBuffer                    Ecb;
            public float                                  DeltaTime;

            void Execute(Entity entity, ref LocalTransform transform, in GoldCoin coin)
            {
                // Find nearest player within magnet radius
                int   nearestIdx  = -1;
                float nearestDist = float.MaxValue;

                for (int i = 0; i < PlayerTransforms.Length; i++)
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
                    float mult = nearestIdx < PlayerGoldMults.Length ? PlayerGoldMults[nearestIdx] : 1f;
                    GoldAccum.Value += (int)math.round(coin.Value * mult);
                    Ecb.DestroyEntity(entity);
                }
                else
                {
                    // Move toward player
                    float3 dir = math.normalizesafe(PlayerTransforms[nearestIdx].Position - transform.Position);
                    transform.Position += new float3(dir.x, dir.y, 0f) * CoinSpeed * DeltaTime;
                }
            }
        }
    }
}
