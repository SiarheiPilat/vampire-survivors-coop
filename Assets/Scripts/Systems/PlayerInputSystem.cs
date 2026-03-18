using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Reads left-stick input from each connected gamepad and writes it into MoveInput.
    /// Not Burst-compiled — reads managed Gamepad.all.
    /// Runs before PlayerMovementSystem (same SimulationSystemGroup, UpdateBefore enforced).
    /// </summary>
    [UpdateBefore(typeof(PlayerMovementSystem))]
    public partial struct PlayerInputSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (index, moveInput) in
                SystemAPI.Query<RefRO<PlayerIndex>, RefRW<MoveInput>>())
            {
                int i = index.ValueRO.Value;
                if (Gamepad.all.Count > i)
                {
                    // Vector2 implicitly converts to float2
                    moveInput.ValueRW.Value = Gamepad.all[i].leftStick.ReadValue();
                }
                else
                {
                    // Gamepad not connected — player stays still
                    moveInput.ValueRW.Value = float2.zero;
                }
            }
        }
    }
}
