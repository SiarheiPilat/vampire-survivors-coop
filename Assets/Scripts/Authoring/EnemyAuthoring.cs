using Unity.Entities;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Authoring
{
    /// <summary>
    /// Attach to each enemy prefab GameObject.
    /// Baker stamps EnemyTag, EnemyStats, and Health onto the entity.
    /// </summary>
    public class EnemyAuthoring : MonoBehaviour
    {
        [Header("Enemy Stats")]
        public int hp;
        public float moveSpeed;
        public int contactDamage;
        public int xpValue;

        class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new EnemyTag());
                AddComponent(entity, new EnemyStats
                {
                    MoveSpeed     = authoring.moveSpeed,
                    ContactDamage = authoring.contactDamage,
                    XpValue       = authoring.xpValue
                });
                AddComponent(entity, new Health
                {
                    Current = authoring.hp,
                    Max     = authoring.hp
                });
            }
        }
    }
}
