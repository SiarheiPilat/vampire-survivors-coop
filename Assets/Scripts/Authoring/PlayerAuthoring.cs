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
                    XpToNextLevel = 100f
                });
                AddComponent(entity, new AssignedDeviceId { Value = 0 }); // 0 = unassigned; set by GameSceneBootstrap when coming from lobby
            }
        }
    }
}
