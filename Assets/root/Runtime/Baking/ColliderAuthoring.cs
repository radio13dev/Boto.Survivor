using System;
using Collisions;
using Drawing;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using AABB = NativeTrees.AABB;
using Collider = Collisions.Collider;

public struct EnableColliderOnDestroy : IComponentData
{
    
}

public class ColliderAuthoring : MonoBehaviourGizmos
{
    public bool EnableColliderOnDestroy;
    
    public float Radius = 1;
    public ColliderType ColliderType = ColliderType.AABB;
    
    public float TorusMin;
    public float ConeAngle;
    public float3 ConeDirection = math.forward();

    public Collider Collider
    {
        get
        {
            switch (ColliderType)
            {
                case ColliderType.AABB:
                    return Collisions.Collider.DefaultAABB(Radius);
                    break;
                case ColliderType.Sphere:
                    return Collisions.Collider.Sphere(Radius);
                    break;
                case ColliderType.Torus:
                    return Collisions.Collider.Torus(TorusMin, Radius);
                    break;
                case ColliderType.TorusCone:
                    return Collisions.Collider.TorusCone(TorusMin, Radius, ConeAngle, ConeDirection);
                    break;
                default:
                    throw new NotImplementedException("Unknown collider type: " + ColliderType);
            }
        }
    }

    public partial class Baker : Baker<ColliderAuthoring>
    {
        public override void Bake(ColliderAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, authoring.Collider);
            if (authoring.EnableColliderOnDestroy)
            {
                AddComponent<EnableColliderOnDestroy>(entity);
                SetComponentEnabled<Collisions.Collider>(entity, false);
            }
        }
    }

    public override void DrawGizmos()
    {
        var draw = Draw.editor;
        Collider.Apply(LocalTransform.FromPositionRotationScale(transform.position, transform.rotation, transform.localScale.x)).DebugDraw(draw, Color.white);
    }
}