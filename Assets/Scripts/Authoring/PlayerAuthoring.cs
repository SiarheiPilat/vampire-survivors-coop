using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using VampireSurvivors.Components;

namespace VampireSurvivors.Authoring
{
    /// <summary>
    /// Add to a scene GameObject. Baker converts it to a player ECS entity at bake time.
    /// Assign playerMesh (a Quad mesh asset) and playerMaterials (4 colored materials) in the Inspector.
    /// </summary>
    public class PlayerAuthoring : MonoBehaviour
    {
        [Header("Player Config")]
        public byte playerIndex;
        public float moveSpeed = 7f;
        public int maxHp = 100;

        [Header("Visuals")]
        [Tooltip("Assign a quad mesh (e.g. Unity built-in Quad)")]
        public Mesh playerMesh;
        [Tooltip("4 materials: index 0=Red, 1=Blue, 2=Green, 3=Yellow")]
        public Material[] playerMaterials = new Material[4];

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

                // Colored quad visual
                var material = authoring.playerMaterials[authoring.playerIndex];
                var desc = new RenderMeshDescription(ShadowCastingMode.Off, receiveShadows: false);
                var rma  = new RenderMeshArray(new[] { material }, new[] { authoring.playerMesh });
                RenderMeshUtility.AddComponents(
                    entity, this, desc, rma,
                    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
            }
        }
    }
}
