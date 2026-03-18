using Unity.Entities;
using Unity.Mathematics;

namespace VampireSurvivors.Components
{
    /// <summary>Zero-size marker — identifies all player entities.</summary>
    public struct PlayerTag : IComponentData { }

    /// <summary>Maps this entity to Gamepad.all[Value].</summary>
    public struct PlayerIndex : IComponentData
    {
        public byte Value;
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
