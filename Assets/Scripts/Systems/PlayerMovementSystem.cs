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

            void Execute(in MoveInput input, in MoveSpeed speed, in PlayerStats stats,
                         ref LocalTransform transform, ref FacingDirection facing)
            {
                // Move in XY plane only; Z stays 0; SpeedMult from Wings passive
                float effectiveSpeed = speed.Value * stats.SpeedMult;
                transform.Position += new float3(input.Value * effectiveSpeed * DeltaTime, 0f);

                // Update facing when there is input; keep last known direction otherwise
                if (math.lengthsq(input.Value) > 0.001f)
                    facing.Value = math.normalize(input.Value);
            }
        }
    }
}
