using Unity.Entities;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.Authoring
{
    /// <summary>
    /// Add to a pickup prefab (alongside MeshFilter + MeshRenderer for visuals).
    /// Baker stamps the appropriate IComponentData so pickup systems can recognise
    /// and collect it. Value fields (XpGem.Value, GoldCoin.Value, HealthPickup.HealAmount)
    /// are overwritten at spawn time by HealthSystem via ECB.SetComponent.
    /// </summary>
    public class PickupAuthoring : MonoBehaviour
    {
        public enum PickupKind { XpGem, GoldCoin, HealthPickup, MagnetPickup, Chest, OrologionPickup, BombPickup }
        public PickupKind kind;

        class Baker : Baker<PickupAuthoring>
        {
            public override void Bake(PickupAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                switch (authoring.kind)
                {
                    case PickupKind.XpGem:
                        AddComponent(entity, new XpGem { Value = 0f });
                        break;
                    case PickupKind.GoldCoin:
                        AddComponent(entity, new GoldCoin { Value = 0 });
                        break;
                    case PickupKind.HealthPickup:
                        AddComponent(entity, new HealthPickup { HealAmount = 0 });
                        break;
                    case PickupKind.MagnetPickup:
                        AddComponent(entity, new MagnetPickup());
                        break;
                    case PickupKind.Chest:
                        AddComponent(entity, new Chest
                        {
                            // Rng seeded with entity index; overwritten by HealthSystem at spawn
                            Rng = Unity.Mathematics.Random.CreateFromIndex(0)
                        });
                        break;
                    case PickupKind.OrologionPickup:
                        AddComponent(entity, new OrologionPickup());
                        break;
                    case PickupKind.BombPickup:
                        AddComponent(entity, new BombPickup());
                        break;
                }
            }
        }
    }
}
