using Unity.Entities;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Authoring
{
    /// <summary>
    /// Attach to a scene GameObject (e.g. "BulletPrefabSpawner").
    /// Wire bulletPrefab to Assets/Prefabs/Bullet.prefab in the Inspector.
    /// Baker bakes a singleton BulletPrefabData entity so projectile weapons can
    /// look up the prefab entity at runtime via SystemAPI.GetSingleton<BulletPrefabData>().
    /// </summary>
    public class BulletPrefabAuthoring : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject bulletPrefab;

        class Baker : Baker<BulletPrefabAuthoring>
        {
            public override void Bake(BulletPrefabAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BulletPrefabData
                {
                    BulletPrefab = GetEntity(authoring.bulletPrefab, TransformUsageFlags.Dynamic)
                });
            }
        }
    }
}
