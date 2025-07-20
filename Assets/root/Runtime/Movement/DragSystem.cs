using System;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(MovementSystemGroup))]
public partial struct DragSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        new DragJob()
        {
            dt = SystemAPI.Time.DeltaTime
        }.Schedule();
        new SurfaceDragJob()
        {
            dt = SystemAPI.Time.DeltaTime
        }.Schedule();
    }
    
    partial struct DragJob : IJobEntity
    {
        [ReadOnly] public float dt;
    
        public void Execute(ref Movement movement, ref MovementSettings drag)
        {
            // Relative drag
            if (drag.Drag != 0) movement.Velocity -= movement.Velocity*dt*drag.Drag;
            // Linear drag
            if (drag.LinearDrag != 0) movement.Velocity = mathu.MoveTowards(movement.Velocity, float3.zero, drag.LinearDrag*dt);
        }
    }
    
    partial struct SurfaceDragJob : IJobEntity
    {
        [ReadOnly] public float dt;
    
        public void Execute(ref SurfaceMovement movement, ref MovementSettings drag)
        {
            // Relative drag
            if (drag.Drag != 0) movement.Velocity -= movement.Velocity*dt*drag.Drag;
            // Linear drag
            if (drag.LinearDrag != 0) movement.Velocity = mathu.MoveTowards(movement.Velocity, float2.zero, drag.LinearDrag*dt);
        }
    }
}