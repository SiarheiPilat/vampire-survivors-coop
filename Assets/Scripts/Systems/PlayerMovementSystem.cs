using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Moves player entities based on MoveInput and MoveSpeed.
    /// Burst-compiled — no managed references.
    /// </summary>
    [BurstCompile]
    public partial struct PlayerMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new MoveJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct MoveJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(in MoveInput input, in MoveSpeed speed, ref LocalTransform transform)
            {
                // Move in XY plane only; Z stays 0
                transform.Position += new float3(input.Value * speed.Value * DeltaTime, 0f);
            }
        }
    }
}
