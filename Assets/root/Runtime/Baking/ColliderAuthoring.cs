using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using AABB = NativeTrees.AABB;

public struct EnableColliderOnDestroy : IComponentData
{
    
}

public class ColliderAuthoring : MonoBehaviour
{
    public float Radius = 1;
    public bool EnableColliderOnDestroy;

    public partial class Baker : Baker<ColliderAuthoring>
    {
        public override void Bake(ColliderAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new Collisions.Collider(new AABB(-authoring.Radius, authoring.Radius)));
            if (authoring.EnableColliderOnDestroy)
            {
                AddComponent<EnableColliderOnDestroy>(entity);
                SetComponentEnabled<Collisions.Collider>(entity, false);
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1f, 0.3f, 1f);
        Gizmos.DrawWireSphere(transform.position, Radius*math.SQRT2);
        Gizmos.color = new Color(0, 1f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, Radius);
    }
}