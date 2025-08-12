using System;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;


/// <summary>
/// Projectile collision is predicted, only after all movement is done
/// </summary>
[UpdateBefore(typeof(MovementSystemGroup))]
[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
public partial class ProcessInputsSystemGroup : ComponentSystemGroup
{
}

[Save]
[Serializable]
public struct MovementSettings : IComponentData
{
    public float Speed;
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
        state.Dependency = new MovementInputJob()
        {
        }.ScheduleParallel(state.Dependency);
        state.Dependency = new RollInputJob()
        {
        }.ScheduleParallel(state.Dependency);
    }

    [WithNone(typeof(MovementInputLockout))]
    [WithAll(typeof(Simulate))]
    partial struct MovementInputJob : IJobEntity
    {
        public void Execute(in StepInput input, ref LocalTransform local, ref Movement movement, in MovementSettings movementSettings)
        {
            // Rotate character to face input direction
            var up = local.Up();
            var inputForward = math.cross(up, math.cross(math.normalizesafe(input.Direction, local.Forward()), up));
            local.Rotation = quaternion.LookRotation(inputForward, up);
        
            movement.LastDirection = math.normalizesafe(inputForward, movement.LastDirection);
            var vel = movement.LastDirection * movementSettings.Speed * math.clamp(math.length(input.Direction), 0, 1);
            movement.Velocity += vel;
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