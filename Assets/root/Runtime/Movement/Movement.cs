using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

/// <summary>
/// Character movement is predicted
/// </summary>
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
public partial class MovementSystemGroup : ComponentSystemGroup
{
        
}

[GhostComponent]
public struct Movement : IComponentData
{
    public float2 Velocity;
    public float Drag;
    public float LinearDrag;
    public float Speed;
    
    public Movement(float drag, float linearDrag)
    {
        Velocity = float2.zero;
        Drag = drag;
        LinearDrag = linearDrag;
        Speed = 1;
    }
}

[UpdateInGroup(typeof(MovementSystemGroup))]
public partial struct MovementSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        new Job()
        {
            dt = SystemAPI.Time.DeltaTime
        }.Schedule();
    }
    
    partial struct Job : IJobEntity
    {
        [ReadOnly] public float dt;
    
        public void Execute(Entity entity, ref Movement movement, ref LocalTransform transform)
        {
            transform.Position += (movement.Velocity*dt).f3();
            
            // Relative drag
            movement.Velocity -= movement.Velocity*dt*movement.Drag;
            // Linear drag
            movement.Velocity = mathu.MoveTowards(movement.Velocity, float2.zero, movement.LinearDrag*dt);
        }
    }
}