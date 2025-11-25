using Unity.Entities;
using UnityEngine;

public class SurvivorSpawnPointAuthoring : MonoBehaviour
{
    partial class Baker : Baker<SurvivorSpawnPointAuthoring>
    {
        public override void Bake(SurvivorSpawnPointAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent<SurvivorSpawnPoint>(entity);
        }
    }
}

public struct SurvivorSpawnPoint : IComponentData { }
