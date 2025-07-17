using NativeTrees;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ColliderAuthoring : MonoBehaviour
{
    public float2 Min = new float2(-0.5f,-0.5f);
    public float2 Max = new float2(0.5f,0.5f);

    public partial class Baker : Baker<ColliderAuthoring>
    {
        public override void Bake(ColliderAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new Collisions.Collider(new AABB2D(authoring.Min, authoring.Max)));
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1f, 0.3f, 1f);
        Gizmos.DrawWireCube(transform.position + (Vector3)(Min/2 + Max/2).f3(), (Max - Min).f3());
    }
}