using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Similar to Movement. Jobs can add velocities to this and they'll be applied to the Movement component next frame.
/// </summary>
public struct Force : IComponentData
{
    public float2 Velocity;
    public float2 Shift;
    
    public void Reset()
    {
        Velocity = default;
        Shift = default;
    }
}

[UpdateInGroup(typeof(MovementSystemGroup))]
[UpdateAfter(typeof(MovementSystem))]
public partial struct ForceSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        new Job()
        {
        }.Schedule();
    }
    
    partial struct Job : IJobEntity
    {
        public void Execute(Entity entity, ref LocalTransform transform, ref Movement movement, ref Force force)
        {
            movement.Velocity += force.Velocity;
            transform.Position += force.Shift.f3();
            
            force.Reset();
        }
    }
}