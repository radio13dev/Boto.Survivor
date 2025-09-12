using Collisions;
using Drawing;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Vella.UnityNativeHull;
using Collider = Collisions.Collider;

[UpdateInGroup(typeof(RenderSystemGroup))]
public partial struct ColliderDebugger_RuntimeSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.Enabled = true;
    }

    public unsafe void OnUpdate(ref SystemState state)
    {
        var draw = Draw.ingame;
        using (draw.WithLineWidth(2, false))
        {
            foreach (var playerT in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<SurvivorTag>())
            foreach (var (transform, collider, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<Collider>>().WithEntityAccess().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState))
            {
                if (collider.ValueRO.Type != ColliderType.MeshCollider) continue;
                bool enabled = SystemAPI.IsComponentEnabled<Collider>(entity);
                var c = collider.ValueRO.Apply(transform.ValueRO);
                c.DebugDraw(draw, enabled ? new Color(1,1,1,1f) : new Color(1,1,1,0.5f));
                
               var pClose = HullCollision.ClosestPoint(new RigidTransform(c.MeshTransform.Rotation, 0), ((NativeHull*)NativeHullManager.m_Hulls.Data)[c.MeshPtr], (playerT.ValueRO.Position-c.MeshTransform.Position)/c.MeshTransform.Scale);
               pClose *= c.MeshTransform.Scale;
               pClose += c.MeshTransform.Position;
               draw.Arrow(playerT.ValueRO.Position, pClose);
            }
        }
    }
}
