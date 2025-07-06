using Unity.Entities;
using UnityEngine;

public class GameManagerResourcesAuthoring : MonoBehaviour
{
    public GameObject ProjectileTemplate;

    public class Baker : Baker<GameManagerResourcesAuthoring>
    {
        public override void Bake(GameManagerResourcesAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new GameManager.Resources()
            {
                ProjectileTemplate = GetEntity(authoring.ProjectileTemplate, TransformUsageFlags.WorldSpace),
            });
        }
    }
}