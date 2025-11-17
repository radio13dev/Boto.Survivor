using Unity.Entities;
using UnityEngine;

public class LevelUpInteractableAuthoring : MonoBehaviour
{
    partial class Baker : Baker<LevelUpInteractableAuthoring>
    {
        public override void Bake(LevelUpInteractableAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent<Interactable>(entity);
            AddComponent<LevelUpInteractableTag>(entity);
        }
    }
}

public struct LevelUpInteractableTag : IComponentData{}
