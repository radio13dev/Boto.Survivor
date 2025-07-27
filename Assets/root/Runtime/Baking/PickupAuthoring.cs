using UnityEngine;
using Unity.Entities;

public class PickupAuthoring : MonoBehaviour
{
    partial class Baker : Baker<PickupAuthoring>
    {
        public override void Bake(PickupAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new Collectable());
            SetComponentEnabled<Collectable>(entity, false);
            AddComponent(entity, new Collected());
            SetComponentEnabled<Collected>(entity, false);
        }
    }
}