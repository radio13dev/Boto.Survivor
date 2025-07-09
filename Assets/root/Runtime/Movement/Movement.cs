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
    public float2 LastDirection;
    public float Drag;
    public float LinearDrag;
    public float Speed;
    
    public Movement(float drag, float linearDrag, float speed)
    {
        Velocity = float2.zero;
        LastDirection = new float2(1,0);
        Drag = drag;
        LinearDrag = linearDrag;
        Speed = speed;
    }
    public Movement(float2 direction)
    {
        Velocity = direction;
        LastDirection = direction;
        Drag = 0;
        LinearDrag = 0;
        Speed = 0;
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
            if (movement.Drag != 0) movement.Velocity -= movement.Velocity*dt*movement.Drag;
            // Linear drag
            if (movement.LinearDrag != 0) movement.Velocity = mathu.MoveTowards(movement.Velocity, float2.zero, movement.LinearDrag*dt);
        }
    }
}