using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(MovementSystemGroup))]
public partial struct TorusGravitySystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        new Job()
        {
        }.Schedule();
    }
    
    [BurstCompile]
    partial struct Job : IJobEntity
    {
        public void Execute(Entity entity, ref Movement movement, ref LocalTransform transform)
        {
            if (movement.Grounded)
            {
                transform.Position = TorusMapper.SnapToSurface(transform.Position);
                return;
            }
        
            // For every entity check if underground and clamp them to surface
            // Also update their normal
            TorusMapper.ClampAboveSurface(transform.Position, out var clampedPos, out var clamped, out var normalIfClamped);
            if (clamped)
            {
                // Grounding true
                movement.Grounded = true;
                
                // Zero velocity in clamp direction
                movement.Velocity -= math.dot(movement.Velocity, normalIfClamped)*normalIfClamped;
                
                // Snap position to surface
                transform.Position = clampedPos;
            }
        }
    }
}