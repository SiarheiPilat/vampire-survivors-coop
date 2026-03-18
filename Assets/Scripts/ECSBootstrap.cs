using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Attach to any GameObject in your scene. On play, spawns 5 white quad entities
/// visible in the Scene/Game view and in Window > DOTS > Entities Hierarchy.
/// </summary>
public class ECSBootstrap : MonoBehaviour
{
    void Start()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;

        // Grab Unity's built-in quad mesh without leaving a GameObject in the scene
        var tempGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var mesh = tempGO.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tempGO);

        var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));

        var desc = new RenderMeshDescription(ShadowCastingMode.Off, receiveShadows: false);
        var rma  = new RenderMeshArray(new[] { material }, new[] { mesh });

        for (int i = 0; i < 5; i++)
        {
            var entity = em.CreateEntity();
            em.SetName(entity, $"TestEntity_{i}");
            RenderMeshUtility.AddComponents(
                entity, em, desc, rma,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
            em.SetComponentData(entity, LocalTransform.FromPosition(new float3(i * 2f, 0f, 0f)));
        }

        Debug.Log("[ECSBootstrap] Spawned 5 entities. Check Scene view and DOTS > Entities Hierarchy.");
    }
}
