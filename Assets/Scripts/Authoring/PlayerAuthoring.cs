using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Authoring
{
    /// <summary>
    /// Add to a scene GameObject (alongside MeshFilter + MeshRenderer for visuals).
    /// Baker stamps custom ECS components onto the entity; Unity's built-in
    /// MeshRendererBaker handles the rendering components automatically.
    /// </summary>
    public class PlayerAuthoring : MonoBehaviour
    {
        [Header("Player Config")]
        public byte playerIndex;
        public float moveSpeed = 7f;
        public int maxHp = 100;

        class Baker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new PlayerTag());
                AddComponent(entity, new PlayerIndex { Value = authoring.playerIndex });
                AddComponent(entity, new MoveInput { Value = float2.zero });
                AddComponent(entity, new MoveSpeed { Value = authoring.moveSpeed });
                AddComponent(entity, new PlayerStats
                {
                    Hp            = authoring.maxHp,
                    MaxHp         = authoring.maxHp,
                    Level         = 1,
                    Xp            = 0f,
                    XpToNextLevel = 5f,
                    Might         = 1.0f,
                    HpRegen       = 0.0f,
                    HpRegenAccum  = 0.0f,
                    XpMult        = 1.0f,  // Crown increases this by ×1.08 per pickup
                    CooldownMult       = 1.0f,  // Empty Tome reduces this by ×0.92 per pickup
                    Luck               = 0.0f,  // Clover adds +0.1 per pickup; scales enemy drop rates
                    ProjectileSpeedMult= 1.0f,  // Bracer multiplies by 1.1 per pickup
                    AreaMult           = 1.0f,  // Candelabrador multiplies by 1.1 per pickup
                    DurationMult       = 1.0f,  // Spellbinder multiplies by 1.1 per pickup
                    SpeedMult          = 1.0f,  // Wings adds 0.1 per pickup (additive)
                    MagnetRadiusMult   = 1.0f,  // Attractorb multiplies by 1.3 per pickup
                    ProjectileSpeedBonusPerLevel = 0.0f, // Giovanna: 0.01 set by GameSceneBootstrap
                });
                AddComponent(entity, new AssignedDeviceId { Value = 0 });
                AddComponent(entity, new Health { Current = authoring.maxHp, Max = authoring.maxHp });
                AddComponent(entity, new Invincible { Timer = 0f });
                AddComponent(entity, new WeaponState
                {
                    SwingTimer    = 0f,
                    SwingCooldown = 0.5f,
                    Damage        = 10f,
                    Range         = 1.5f,
                    ArcDegrees    = 120f,
                    Amount        = 1
                });
                AddComponent(entity, new FacingDirection { Value = new float2(1f, 0f) });
                // Weapons are unlocked by LevelUpSystem as the player levels up:
                //   Level 2 → Magic Wand (MagicWandState added)
                //   Level 3 → Garlic     (GarlicState added)
                //   Level 4 → Knife      (KnifeState added)
                // Weapon systems naturally activate once their state component exists.
            }
        }
    }
}
