using NativeTrees;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using AABB = NativeTrees.AABB;

public class ColliderAuthoring : MonoBehaviour
{
    public float3 Min = new float3(-0.5f,-0.5f,-0.5f);
    public float3 Max = new float3(0.5f,0.5f,-0.5f);

    public partial class Baker : Baker<ColliderAuthoring>
    {
        public override void Bake(ColliderAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new Collisions.Collider(new AABB(authoring.Min, authoring.Max)));
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1f, 0.3f, 1f);
        Gizmos.DrawWireCube(transform.position + (Vector3)(Min/2 + Max/2), (Max - Min));
    }
}