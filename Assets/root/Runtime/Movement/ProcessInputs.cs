using System;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


/// <summary>
/// Projectile collision is predicted, only after all movement is done
/// </summary>
[UpdateBefore(typeof(MovementSystemGroup))]
public partial class ProcessInputsSystemGroup : ComponentSystemGroup
{
}

[Save]
[Serializable]
public struct MovementSettings : IComponentData
{
    public float Speed;
    public float Drag;
    public float LinearDrag;
    
    public MovementSettings(float speed, float drag, float linearDrag)
    {
        Speed = speed;
        Drag = drag;
        LinearDrag = linearDrag;
    }
}

[Save]
public struct LastStepInputLastDirection : IComponentData
{
    public float2 Value;
}

[Save]
public struct MovementInputLockout : IComponentData, IEnableableComponent
{
}

[UpdateInGroup(typeof(ProcessInputsSystemGroup))]
public partial struct ProcessInputs : ISystem
{
    static readonly float2 DirMin = new float2(-1, -1);
    static readonly float2 DirMax = new float2(1, 1);

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<StepInput>();
    }

    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new MovementInputJob().Schedule(state.Dependency);
        state.Dependency = new RotateInputJob()
        {
            dt = SystemAPI.Time.DeltaTime
        }.Schedule(state.Dependency);
        state.Dependency = new RollInputJob()
        {
        }.Schedule(state.Dependency);
    }

    [WithNone(typeof(MovementInputLockout))]
    [WithAll(typeof(Simulate))]
    partial struct MovementInputJob : IJobEntity
    {
        public void Execute(in StepInput input, ref LastStepInputLastDirection lastDirection, in LocalTransform local, ref Movement movement, in MovementSettings movementSettings)
        {
            lastDirection.Value = math.normalizesafe(input.Direction, lastDirection.Value);
            var dir = local.TransformDirection(input.Direction.f3z());
            var vel = math.normalizesafe(dir) * movementSettings.Speed * math.clamp(math.length(dir), 0, 1);
            movement.Velocity += vel;
            movement.LastDirection = math.normalizesafe(dir, movement.LastDirection);
        }
    }

    [WithAll(typeof(Simulate))]
    partial struct RotateInputJob : IJobEntity
    {
        [ReadOnly] public float dt;
        public void Execute(in StepInput input, ref LocalTransform local)
        {
            var sign = input.RotateSign;
            if (sign != 0)
            {
                local.Rotation = math.mul(local.Rotation, quaternion.RotateY(2.0f*dt*sign));
            }
        }
    }
    
    [WithPresent(typeof(ActiveLockout), typeof(MovementInputLockout), typeof(RollActive))]
    [WithAll(typeof(Simulate))]
    partial struct RollInputJob : IJobEntity
    {
        public void Execute(in StepInput input,
            EnabledRefRW<ActiveLockout> activeLockout, EnabledRefRW<MovementInputLockout> movementInputLockout,
            EnabledRefRW<RollActive> roll)
        {
            if (!activeLockout.ValueRW)
            {
                if (input.S1)
                {
                    roll.ValueRW = true;
                    activeLockout.ValueRW = true;
                    movementInputLockout.ValueRW = true;
                }
            }
        }
    }
}