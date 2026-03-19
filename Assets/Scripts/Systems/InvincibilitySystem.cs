using Unity.Burst;
using Unity.Entities;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Ticks Invincible.Timer down each frame. When Timer reaches 0, the entity
    /// can take contact damage again.
    /// </summary>
    [BurstCompile]
    public partial struct InvincibilitySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            new TickInvincibleJob { DeltaTime = dt }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct TickInvincibleJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(ref Invincible invincible)
            {
                if (invincible.Timer > 0f)
                    invincible.Timer = Unity.Mathematics.math.max(0f, invincible.Timer - DeltaTime);
            }
        }
    }
}
