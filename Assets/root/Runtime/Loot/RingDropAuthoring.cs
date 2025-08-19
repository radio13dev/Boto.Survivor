using Unity.Entities;
using UnityEngine;

public class RingDropAuthoring : MonoBehaviour
{
    partial class Baker : Baker<RingDropAuthoring>
    {
        public override void Bake(RingDropAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent<Interactable>(entity);
            AddComponent<RingStats>(entity);
            AddSharedComponent<LootKey>(entity, new LootKey());
        }
    }
}