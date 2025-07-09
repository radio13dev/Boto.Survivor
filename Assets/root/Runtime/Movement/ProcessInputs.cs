using System.Diagnostics.Contracts;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

/// <summary>
/// Projectile collision is predicted, only after all movement is done
/// </summary>
[UpdateBefore(typeof(MovementSystemGroup))]
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
public partial class ProcessInputsSystemGroup : ComponentSystemGroup
{
        
}

public struct MovementInputLockout : IComponentData, IEnableableComponent { }

[UpdateInGroup(typeof(ProcessInputsSystemGroup))]
public partial struct ProcessInputs : ISystem
{
    static readonly float2 DirMin = new float2(-1,-1);
    static readonly float2 DirMax = new float2(1,1);

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInput>();
    }

    public void OnUpdate(ref SystemState state)
    {
        new MovementJob()
        {
        }.Schedule();
        new ActiveJob()
        {
        }.Schedule();
    }
    
    [WithAll(typeof(Simulate))]
    partial struct MovementJob : IJobEntity
    {
        public void Execute(in PlayerInput input, ref Movement movement)
        {
            var vel = math.clamp(input.Dir, DirMin, DirMax)*movement.Speed;
            movement.Velocity += vel;
            movement.LastDirection = math.normalizesafe(input.Dir, movement.LastDirection);
        }
    }
    [WithAll(typeof(Simulate))]
    partial struct ActiveJob : IJobEntity
    {
        public void Execute(in PlayerInput input,
            EnabledRefRW<ActiveLockout> activeLockout, EnabledRefRW<MovementInputLockout> movementInputLockout,
            EnabledRefRW<RollActive> roll)
        {
            if (!activeLockout.ValueRW)
            {
                if (input.RollActive.IsSet)
                {
                    roll.ValueRW = true;
                    activeLockout.ValueRW = true;
                    movementInputLockout.ValueRW = true;
                }
            }
        }
    }
}