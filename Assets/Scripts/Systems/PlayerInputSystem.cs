using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Reads input for each player and writes it into MoveInput.
    /// Keyboard + gamepad are additive and normalized, so either works at any time.
    ///   Player 0 — WASD + Gamepad[0]
    ///   Player 1 — Arrow keys + Gamepad[1]
    ///   Player 2+ — Gamepad[n] only
    /// Not Burst-compiled — reads managed Input System APIs.
    /// Runs before PlayerMovementSystem.
    /// </summary>
    [UpdateBefore(typeof(PlayerMovementSystem))]
    public partial struct PlayerInputSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var keyboard = Keyboard.current;

            foreach (var (index, moveInput) in
                SystemAPI.Query<RefRO<PlayerIndex>, RefRW<MoveInput>>())
            {
                int i = index.ValueRO.Value;
                float2 dir = float2.zero;

                // Keyboard input
                if (keyboard != null)
                {
                    if (i == 0)
                    {
                        if (keyboard.wKey.isPressed) dir.y += 1f;
                        if (keyboard.sKey.isPressed) dir.y -= 1f;
                        if (keyboard.aKey.isPressed) dir.x -= 1f;
                        if (keyboard.dKey.isPressed) dir.x += 1f;
                    }
                    else if (i == 1)
                    {
                        if (keyboard.upArrowKey.isPressed)    dir.y += 1f;
                        if (keyboard.downArrowKey.isPressed)  dir.y -= 1f;
                        if (keyboard.leftArrowKey.isPressed)  dir.x -= 1f;
                        if (keyboard.rightArrowKey.isPressed) dir.x += 1f;
                    }
                }

                // Gamepad input — additive on top of keyboard
                if (Gamepad.all.Count > i)
                    dir += (float2)Gamepad.all[i].leftStick.ReadValue();

                // Normalize so diagonal keyboard + any gamepad drift stays unit length
                moveInput.ValueRW.Value = math.lengthsq(dir) > 1f ? math.normalize(dir) : dir;
            }
        }
    }
}
