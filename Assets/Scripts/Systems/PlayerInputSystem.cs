using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Reads input for each player entity and writes it into MoveInput.
    ///
    /// Device lookup priority:
    ///   1. If the entity has AssignedDeviceId (set by GameSceneBootstrap from
    ///      GameSession), look up that specific device via InputSystem.GetDeviceById.
    ///   2. Otherwise fall back to Gamepad.all[PlayerIndex] — used during dev when
    ///      the game scene is loaded directly without going through the lobby.
    ///
    /// Keyboard fallback (always active regardless of device assignment):
    ///   Player 0 — WASD
    ///   Player 1 — Arrow keys
    ///
    /// Not Burst-compiled — reads managed Input System APIs.
    /// Runs before PlayerMovementSystem.
    /// </summary>
    [UpdateBefore(typeof(PlayerMovementSystem))]
    public partial struct PlayerInputSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var keyboard = Keyboard.current;

            foreach (var (index, assignedDevice, moveInput) in
                SystemAPI.Query<RefRO<PlayerIndex>, RefRO<AssignedDeviceId>, RefRW<MoveInput>>()
                         .WithOptions(EntityQueryOptions.Default))
            {
                int i = index.ValueRO.Value;
                float2 dir = float2.zero;

                // Keyboard fallback
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

                // Assigned device (from GameSession via GameSceneBootstrap)
                int deviceId = assignedDevice.ValueRO.Value;
                if (deviceId != 0)
                {
                    var device = InputSystem.GetDeviceById(deviceId);
                    if (device is Gamepad gamepad)
                        dir += (float2)gamepad.leftStick.ReadValue();
                }
                else if (Gamepad.all.Count > i)
                {
                    // Dev fallback: no session, use Gamepad.all[i]
                    dir += (float2)Gamepad.all[i].leftStick.ReadValue();
                }

                moveInput.ValueRW.Value = math.lengthsq(dir) > 1f ? math.normalize(dir) : dir;
            }
        }
    }
}
