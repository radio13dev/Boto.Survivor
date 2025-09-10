using Collisions;
using Drawing;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Collider = Collisions.Collider;

[UpdateInGroup(typeof(RenderSystemGroup))]
public partial struct ColliderDebugger_RuntimeSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var draw = Draw.ingame;
        using (draw.WithLineWidth(2, false))
        {
            foreach (var (transform, collider, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<Collider>>().WithEntityAccess().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState))
            {
                bool enabled = SystemAPI.IsComponentEnabled<Collider>(entity);
                var c = collider.ValueRO.Apply(transform.ValueRO);
                c.DebugDraw(draw, enabled ? new Color(1,1,1,1f) : new Color(1,1,1,0.5f));
            }
        }
    }
}
