using Unity.Entities;
using UnityEngine;

public static class GameManager
{
    [ChunkSerializable]
    public struct Resources : IComponentData
    {
        public Entity ProjectileTemplate;
        public Entity SurvivorTemplate;
    }
}

public class GameManagerResourcesAuthoring : MonoBehaviour
{
    public GameObject ProjectileTemplate;
    public GameObject SurvivorTemplate;

    public class Baker : Baker<GameManagerResourcesAuthoring>
    {
        public override void Bake(GameManagerResourcesAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new GameManager.Resources()
            {
                ProjectileTemplate = GetEntity(authoring.ProjectileTemplate, TransformUsageFlags.WorldSpace),
                SurvivorTemplate = GetEntity(authoring.SurvivorTemplate, TransformUsageFlags.WorldSpace),
            });
        }
    }
}