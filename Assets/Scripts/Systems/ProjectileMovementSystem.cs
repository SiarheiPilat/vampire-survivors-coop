using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Moves all Projectile entities along their Direction vector each frame.
    /// Destroys the entity when Traveled >= MaxRange.
    /// Runs single-threaded to avoid write races with ProjectileHitSystem.
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(MagicWandSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ProjectileMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (proj, transform, entity) in
                SystemAPI.Query<RefRW<Projectile>, RefRW<LocalTransform>>()
                    .WithEntityAccess())
            {
                float3 move = proj.ValueRO.Direction * proj.ValueRO.Speed * dt;
                transform.ValueRW.Position += new float3(move.x, move.y, 0f);
                proj.ValueRW.Traveled      += math.length(move);

                if (proj.ValueRO.Traveled >= proj.ValueRO.MaxRange)
                    ecb.DestroyEntity(entity);
            }
        }
    }
}
