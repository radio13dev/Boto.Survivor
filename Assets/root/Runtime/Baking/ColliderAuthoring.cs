using System;
using Collisions;
using Drawing;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Vella.UnityNativeHull;
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
    public DatabaseRef<Mesh, MeshDatabase> Mesh = new();

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
                case ColliderType.MeshCollider:
                    if (!Mesh.Asset) return default;
                    return Collisions.Collider.MeshCollider(Mesh);
                default:
                    throw new NotImplementedException("Unknown collider type: " + ColliderType);
            }
        }
    }

    public partial class Baker : Baker<ColliderAuthoring>
    {
        public override void Bake(ColliderAuthoring authoring)
        {
            if (authoring.ColliderType == ColliderType.MeshCollider && !authoring.Mesh.Asset)
            {
                Debug.LogError($"Invalid mesh collider on {authoring.name}: No mesh assigned");
                return;
            }

            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, authoring.Collider);
            if (authoring.EnableColliderOnDestroy)
            {
                AddComponent<EnableColliderOnDestroy>(entity);
                SetComponentEnabled<Collisions.Collider>(entity, false);
            }
        }
    }

    NativeHull m_EditorMesh;

    private void OnDestroy()
    {
        if (m_EditorMesh.IsCreated) m_EditorMesh.Dispose();
    }

    public override unsafe void DrawGizmos()
    {
        var draw = Draw.editor;
        var c = Collider.Apply(LocalTransform.FromPositionRotationScale(transform.position, transform.rotation, transform.lossyScale.x));
        c.DebugDraw(draw, Color.white);

        if (ColliderType == ColliderType.MeshCollider)
        {
            if (!Mesh.Asset)
            {
                draw.WireSphere(transform.position, 3, Color.red);
                return; // Invalid
            }
            if (TryGetComponent<InstancedResourceAuthoring>(out var resourceAuthoring))
                if (resourceAuthoring.Particle.Asset && Mesh.Asset != resourceAuthoring.Particle.Asset.Mesh)
                {
                    draw.WireSphere(transform.position, 3, Color.yellow);
                }

            // Draw lines from each corner of the AABB to the closest point on the mesh
            if (!m_EditorMesh.IsCreated) m_EditorMesh = HullFactory.CreateFromMesh(Mesh.Asset);

            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                var p = c.Center + math.float3(c.Radius) * math.float3(x, y, z);
                var pClose = HullCollision.ClosestPoint(new RigidTransform(transform.rotation, 0), m_EditorMesh, ((p - (float3)transform.position) / transform.lossyScale.x));
                pClose *= transform.lossyScale.x;
                pClose += (float3)transform.position;
                draw.DashedLine(p, pClose, 2, 1);
            }
        }
    }
}