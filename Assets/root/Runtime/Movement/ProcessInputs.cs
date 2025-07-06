using Unity.Entities;
using Unity.NetCode;

/// <summary>
/// Projectile collision is predicted, only after all movement is done
/// </summary>
[UpdateBefore(typeof(MovementSystemGroup))]
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
public partial class ProcessInputsSystemGroup : ComponentSystemGroup
{
        
}

[UpdateInGroup(typeof(ProcessInputsSystemGroup))]
public partial struct ProcessInputs : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInput>();
    }

    public void OnUpdate(ref SystemState state)
    {
        
    }
    
    [WithAll(typeof(Simulate))]
    partial struct Job : IJobEntity
    {
        public void Execute(in PlayerInput input, ref Movement movement)
        {
            movement.Velocity += input.Dir*movement.Speed;
        }
    }
}