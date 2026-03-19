using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Reads input for each player entity and writes it into MoveInput.
    ///
    /// Device lookup priority:
    ///   1. If AssignedDeviceId.Value != 0 (set by GameSceneBootstrap from GameSession),
    ///      look up the specific device via InputSystem.GetDeviceById and read leftStick.
    ///      Keyboard is NOT active in this path — device is formally assigned.
    ///   2. Otherwise (Value == 0, dev path): read Gamepad.all[PlayerIndex] and
    ///      keyboard fallback (WASD for P0, arrow keys for P1).
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
                SystemAPI.Query<RefRO<PlayerIndex>, RefRO<AssignedDeviceId>, RefRW<MoveInput>>())
            {
                int i = index.ValueRO.Value;
                float2 dir = float2.zero;

                int deviceId = assignedDevice.ValueRO.Value;
                if (deviceId != 0)
                {
                    // Lobby path — use the device assigned through GameSceneBootstrap
                    var device = InputSystem.GetDeviceById(deviceId);
                    if (device is Gamepad gamepad)
                    {
                        dir += (float2)gamepad.leftStick.ReadValue();
                    }
                    else
                    {
                        Debug.LogWarning($"[PlayerInputSystem] Player {i}: assigned deviceId {deviceId} is not a Gamepad (device={device}).");
                    }
                }
                else
                {
                    // Dev fallback — keyboard + Gamepad.all[i]
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

                    if (Gamepad.all.Count > i)
                        dir += (float2)Gamepad.all[i].leftStick.ReadValue();
                }

                moveInput.ValueRW.Value = math.lengthsq(dir) > 1f ? math.normalize(dir) : dir;
            }
        }
    }
}
