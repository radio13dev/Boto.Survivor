using NativeTrees;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class PickupAuthoring : MonoBehaviour
{
    partial class Baker : Baker<PickupAuthoring>
    {
        public override void Bake(PickupAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new Collectable());
            SetComponentEnabled<Collectable>(entity, false);
        }
    }
}