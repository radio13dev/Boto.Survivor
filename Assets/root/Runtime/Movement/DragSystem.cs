using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(MovementSystemGroup))]
public partial struct DragSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        new GroundedFrictionJob()
        {
            dt = SystemAPI.Time.DeltaTime
        }.Schedule();
        new SurfaceFrictionJob()
        {
            dt = SystemAPI.Time.DeltaTime
        }.Schedule();
        new AirDragJob()
        {
            dt = SystemAPI.Time.DeltaTime
        }.Schedule();
    }
    
    [WithAll(typeof(Grounded))]
    partial struct GroundedFrictionJob : IJobEntity
    {
        [ReadOnly] public float dt;
    
        public void Execute(ref Movement movement, ref PhysicsResponse physicsResponse)
        {
            // Relative drag
            if (physicsResponse.Friction != 0) movement.Velocity -= movement.Velocity*dt*physicsResponse.Friction;
            // Linear drag
            if (physicsResponse.FrictionLinear != 0) movement.Velocity = mathu.MoveTowards(movement.Velocity, float3.zero, physicsResponse.FrictionLinear*dt);
        }
    }
    
    partial struct SurfaceFrictionJob : IJobEntity
    {
        [ReadOnly] public float dt;
    
        public void Execute(ref SurfaceMovement movement, in PhysicsResponse physicsResponse)
        {
            // Relative drag
            if (physicsResponse.Friction != 0) movement.Velocity -= movement.Velocity*dt*physicsResponse.Friction;
            // Linear drag
            if (physicsResponse.FrictionLinear != 0) movement.Velocity = mathu.MoveTowards(movement.Velocity, float2.zero, physicsResponse.FrictionLinear*dt);
        }
    }
    
    [WithNone(typeof(Grounded))]
    partial struct AirDragJob : IJobEntity
    {
        [ReadOnly] public float dt;
    
        public void Execute(ref Movement movement, in PhysicsResponse physicsResponse)
        {
            // Relative drag
            if (physicsResponse.AirDrag != 0) movement.Velocity -= movement.Velocity*dt*physicsResponse.AirDrag;
            // Linear drag
            if (physicsResponse.AirDragLinear != 0) movement.Velocity = mathu.MoveTowards(movement.Velocity, float3.zero, physicsResponse.AirDragLinear*dt);
        }
    }
}