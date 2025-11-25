using Unity.Entities;
using UnityEngine;

public class DestroyableAuthoring : MonoBehaviour
{
    partial class Baker : Baker<DestroyableAuthoring>
    {
        public override void Bake(DestroyableAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent<DestroyFlag>(entity);
            SetComponentEnabled<DestroyFlag>(entity, false);
        }
    }
}