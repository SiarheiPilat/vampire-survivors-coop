using Unity.Entities;
using Unity.Mathematics;

namespace VampireSurvivors.Components
{
    /// <summary>Zero-size marker — identifies all player entities.</summary>
    public struct PlayerTag : IComponentData { }

    /// <summary>
    /// Slot index (0–3) assigned at bake time or by GameSceneBootstrap.
    /// NOT a Gamepad.all index — use AssignedDeviceId for device lookup.
    /// </summary>
    public struct PlayerIndex : IComponentData
    {
        public byte Value;
    }

    /// <summary>
    /// Set by GameSceneBootstrap from GameSession. Stores InputDevice.deviceId
    /// so PlayerInputSystem can look up the correct device regardless of
    /// Gamepad.all connection order.
    /// Value == 0 means "unassigned" — baked entities get this sentinel so the
    /// dev fallback path (Gamepad.all[i]) remains reachable.
    /// </summary>
    public struct AssignedDeviceId : IComponentData
    {
        public int Value; // InputDevice.deviceId; 0 = unassigned
    }

    /// <summary>Current frame's movement input — written by PlayerInputSystem, read by PlayerMovementSystem.</summary>
    public struct MoveInput : IComponentData
    {
        public float2 Value;
    }

    /// <summary>Base movement speed in world units per second.</summary>
    public struct MoveSpeed : IComponentData
    {
        public float Value;
    }

    /// <summary>Player stats — components are live now, systems come later.</summary>
    public struct PlayerStats : IComponentData
    {
        public int Hp;
        public int MaxHp;
        public int Level;
        public float Xp;
        public float XpToNextLevel;
    }
}
