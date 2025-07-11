using Unity.Entities;
using Unity.Mathematics;



/// <summary>
/// Projectile collision is predicted, only after all movement is done
/// </summary>
[UpdateBefore(typeof(MovementSystemGroup))]
public partial class ProcessInputsSystemGroup : ComponentSystemGroup
{
}

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
        new Job()
        {
        }.Schedule();
    }

    [WithPresent(typeof(ActiveLockout), typeof(MovementInputLockout), typeof(RollActive))]
    [WithAll(typeof(Simulate))]
    partial struct Job : IJobEntity
    {
        public void Execute(in StepInput input, ref Movement movement,
            EnabledRefRW<ActiveLockout> activeLockout, EnabledRefRW<MovementInputLockout> movementInputLockout,
            EnabledRefRW<RollActive> roll)
        {
            if (!movementInputLockout.ValueRO)
            {
                var dir = input.Direction;
                var vel = dir * movement.Speed;
                movement.Velocity += vel;
                movement.LastDirection = math.normalizesafe(dir, movement.LastDirection);
            }

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