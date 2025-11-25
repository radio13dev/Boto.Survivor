using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public enum AbilityType
{
    Roll,
    Cylinder,
    Prism
}

[Save]
public struct RollActive : IComponentData, IEnableableComponent
{
    public float T;
    public AbilityType AbilityType;
}

[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
[RequireMatchingQueriesForUpdate]
public partial struct RollActiveSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<RollActive>();
        state.RequireForUpdate<ActiveLockout>();
        state.RequireForUpdate<LocalTransform>();
        state.RequireForUpdate<Movement>();
        state.RequireForUpdate<GameManager.Projectiles>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        new Job()
        {
            dt = SystemAPI.Time.DeltaTime,
            T = SystemAPI.Time.ElapsedTime,
            ecb = delayedEcb,
            projectiles = SystemAPI.GetSingletonBuffer<GameManager.Projectiles>(true)
        }.Schedule();
    }

    [WithPresent(typeof(MovementInputLockout))]
    [WithPresent(typeof(ActiveLockout))]
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer ecb;
        [ReadOnly] public float dt;
        [ReadOnly] public double T;
        [ReadOnly] public DynamicBuffer<GameManager.Projectiles> projectiles;

        public void Execute(EnabledRefRW<RollActive> rollActive, ref RollActive roll, EnabledRefRW<ActiveLockout> activeLockout,
            in LocalTransform entityT, ref Movement movement, in MovementSettings movementSettings,
            EnabledRefRW<MovementInputLockout> movementInputLockout)
        {
            switch (roll.AbilityType)
            {
                case AbilityType.Roll:
                {
                    float speed = roll.T * 10 - 1.5f;
                    speed *= speed;
                    movement.Velocity += movement.LastDirection * math.max(2 - speed, 0.1f) * dt * movementSettings.Speed * 200;

                    roll.T += dt;

                    if (roll.T >= 0.3f)
                    {
                        roll.T = 0;
                        rollActive.ValueRW = false;
                        activeLockout.ValueRW = false;
                        movementInputLockout.ValueRW = false;
                    }

                    break;
                }


                case AbilityType.Cylinder:
                {
                    if (roll.T == 0)
                    {
                        // Init, spawn ability (it'll handle itself during its lifespan)
                        var abilityE = ecb.Instantiate(projectiles[1].Entity);
                        ecb.SetComponent(abilityE, entityT);
                        ecb.SetComponent(abilityE, new DestroyAtTime(){ DestroyTime = T + 2});
                    }
                    else if (roll.T <= 1)
                    {
                    }
                    else if (roll.T <= 1.2f)
                    {
                        // Movement allowed again
                        movementInputLockout.ValueRW = false;
                    }

                    roll.T += dt;

                    if (roll.T >= 3)
                    {
                        // Allow active again, reset T
                        roll.T = 0;
                        rollActive.ValueRW = false;
                        activeLockout.ValueRW = false;
                    }

                    break;
                }
                
                default:
                {
                    roll.T = 0;
                    rollActive.ValueRW = false;
                    activeLockout.ValueRW = false;
                    Debug.LogWarning($"Unimplemented ability type: {roll.AbilityType}");
                    break;
                }
            }
        }
    }
}