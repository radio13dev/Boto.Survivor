using Collisions;
using Drawing;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Collider = Collisions.Collider;

/*
[UpdateInGroup(typeof(RenderSystemGroup))]
public partial struct ColliderDebugger_EditorSystem : ISystem
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
            
                bool isTorus = SystemAPI.HasComponent<TorusMin>(entity);
                if (isTorus)
                {
                    draw.WireSphere(c.Value.Center, c.TorusMin, new Color(1,0,0, enabled ? 0.5f : 0.2f));
                    draw.SphereOutline(c.Value.Center, c.TorusMin, new Color(1,0,0, enabled ? 0.5f : 0.2f));
                }
            
                draw.WireSphere(c.Value.Center, c.Value.Size.x/2, new Color(0.4f,1,0.4f, enabled ? 0.5f : 0.2f));
                draw.SphereOutline(c.Value.Center, c.Value.Size.x/2, new Color(0.4f,1,0.4f, enabled ? 0.5f : 0.2f));
            }
        }
    }
}
*/