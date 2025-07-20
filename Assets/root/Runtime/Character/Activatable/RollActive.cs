using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[Save]
public struct RollActive : IComponentData, IEnableableComponent
{
    public float T;
}

[UpdateInGroup(typeof(GameLogicSystemGroup))]
[RequireMatchingQueriesForUpdate]
public partial struct RollActiveSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<RollActive>();
        state.RequireForUpdate<ActiveLockout>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<Movement>();
    }

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
        public void Execute(EnabledRefRW<RollActive> rollActive, ref RollActive roll, EnabledRefRW<ActiveLockout> activeLockout, 
            in LocalTransform entityT, in LastStepInputLastDirection lastInput, ref Movement movement,
            EnabledRefRW<MovementInputLockout> movementInputLockout)
        {
            float speed = roll.T*10 - 1.5f;
            speed *= speed;
            movement.Velocity += entityT.TransformDirection(lastInput.Value.f3()) * math.max(2-speed,0.1f) * dt;
            
            roll.T += dt;
            
            if (roll.T >= 0.3f)
            {
                roll.T = 0;
                rollActive.ValueRW = false;
                activeLockout.ValueRW = false;
                movementInputLockout.ValueRW = false;
            }
        }
    }
}