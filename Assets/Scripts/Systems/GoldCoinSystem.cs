using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Collects GoldCoin entities when any living player walks within CollectRadius.
    /// No magnet — players must move to coins.
    /// Accumulated value is added to the SharedGold singleton each frame.
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
                .WithAll<PlayerTag, LocalTransform>()
                .WithNone<Downed>()
                .Build();

            if (playerQuery.IsEmpty) return;

            var playerTransforms = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            var goldAccum = new NativeReference<int>(0, Allocator.TempJob);

            new CollectCoinJob
            {
                PlayerTransforms = playerTransforms,
                GoldAccum        = goldAccum,
                Ecb              = ecb
            }.Run();

            playerTransforms.Dispose();

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

        [BurstCompile]
        partial struct CollectCoinJob : IJobEntity
        {
            const float CollectRadius = 0.6f;

            [ReadOnly] public NativeArray<LocalTransform> PlayerTransforms;
            public NativeReference<int>                   GoldAccum;
            public EntityCommandBuffer                    Ecb;

            void Execute(Entity entity, in LocalTransform transform, in GoldCoin coin)
            {
                float nearestDist = float.MaxValue;
                for (int i = 0; i < PlayerTransforms.Length; i++)
                {
                    float dist = math.distance(transform.Position.xy, PlayerTransforms[i].Position.xy);
                    if (dist < nearestDist) nearestDist = dist;
                }

                if (nearestDist <= CollectRadius)
                {
                    GoldAccum.Value += coin.Value;
                    Ecb.DestroyEntity(entity);
                }
            }
        }
    }
}
