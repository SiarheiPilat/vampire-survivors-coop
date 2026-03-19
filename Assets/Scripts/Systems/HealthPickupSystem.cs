using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Collects HealthPickup entities when a living player walks within CollectRadius.
    /// Restores HealAmount HP to the collecting player, capped at Health.Max.
    /// No magnet — players must move to pickups.
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
                .WithAll<PlayerTag, LocalTransform, Health>()
                .WithNone<Downed>()
                .Build();

            if (playerQuery.IsEmpty) return;

            var playerEntities   = playerQuery.ToEntityArray(Allocator.TempJob);
            var playerTransforms = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            new CollectHealthJob
            {
                PlayerEntities   = playerEntities,
                PlayerTransforms = playerTransforms,
                HealthLookup     = _healthLookup,
                Ecb              = ecb
            }.Run();

            playerEntities.Dispose();
            playerTransforms.Dispose();
        }

        [BurstCompile]
        partial struct CollectHealthJob : IJobEntity
        {
            const float CollectRadius = 0.6f;

            [ReadOnly] public NativeArray<Entity>         PlayerEntities;
            [ReadOnly] public NativeArray<LocalTransform> PlayerTransforms;
            [NativeDisableParallelForRestriction] public ComponentLookup<Health> HealthLookup;
            public EntityCommandBuffer Ecb;

            void Execute(Entity entity, in LocalTransform transform, in HealthPickup pickup)
            {
                int   nearestIdx  = -1;
                float nearestDist = float.MaxValue;

                for (int i = 0; i < PlayerEntities.Length; i++)
                {
                    float dist = math.distance(transform.Position.xy, PlayerTransforms[i].Position.xy);
                    if (dist < CollectRadius && dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestIdx  = i;
                    }
                }

                if (nearestIdx < 0) return;

                var hp     = HealthLookup[PlayerEntities[nearestIdx]];
                hp.Current = math.min(hp.Current + pickup.HealAmount, hp.Max);
                HealthLookup[PlayerEntities[nearestIdx]] = hp;
                Ecb.DestroyEntity(entity);
            }
        }
    }
}
