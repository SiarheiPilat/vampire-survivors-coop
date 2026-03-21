using Unity.Burst;
using Unity.Entities;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Ticks down the Frozen.Timer on each frozen enemy every frame.
    /// When the timer reaches zero, removes the Frozen component via ECB so the
    /// enemy can move again (EnemyMovementSystem uses [WithNone(Frozen)]).
    /// </summary>
    [BurstCompile]
    [UpdateAfter(typeof(ClockLancetSystem))]
    [UpdateBefore(typeof(EnemyMovementSystem))]
    public partial struct FrozenTickSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (frozen, entity) in
                SystemAPI.Query<RefRW<Frozen>>().WithEntityAccess())
            {
                frozen.ValueRW.Timer -= dt;
                if (frozen.ValueRO.Timer <= 0f)
                    ecb.RemoveComponent<Frozen>(entity);
            }
        }
    }
}
