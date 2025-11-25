using Drawing;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using AABB = NativeTrees.AABB;
using Collider = Collisions.Collider;

public class CollectableAuthoring : MonoBehaviourGizmos
{
    public float Radius = 1;
    
    partial class Baker : Baker<CollectableAuthoring>
    {
        public override void Bake(CollectableAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            
            // Collectable setup
            AddComponent(entity, new Collectable());
            SetComponentEnabled<Collectable>(entity, false);
            AddComponent(entity, new Collected());
            SetComponentEnabled<Collected>(entity, false);
            AddComponent(entity, new CollectCollider(){Collider = Collider.DefaultAABB(authoring.Radius)});
        }
    }

    public override void DrawGizmos()
    {
        using (Draw.WithLineWidth(2, false))
        {
            Draw.WireSphere(transform.position, Radius*math.SQRT2, new Color(0.5f, 0f, 0.3f, 1f));
            Draw.WireSphere(transform.position, Radius, new Color(0.5f, 0f, 0.3f, 0.5f));
        }
    }
}