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

        [Header("Slime")]
        public bool isSlime;       // big slime — splits on death
        public bool isSmallSlime;  // small slime — no split

        [Header("Boss")]
        public bool isBoss;        // elite/boss — BossTag marker

        [Header("Variant Tags")]
        public bool isGhoul;       // faster undead variant — GhoulTag marker
        public bool isGhost;       // knockback-immune specter — GhostTag marker

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
                AddComponent(entity, new Knockback()); // velocity starts at zero; set by weapon systems on hit
                if (authoring.isSlime)      AddComponent(entity, new SlimeTag());
                if (authoring.isSmallSlime) AddComponent(entity, new SmallSlimeTag());
                if (authoring.isBoss)       AddComponent(entity, new BossTag());
                if (authoring.isGhoul)      AddComponent(entity, new GhoulTag());
                if (authoring.isGhost)      AddComponent(entity, new GhostTag());
            }
        }
    }
}
