using System;
using BovineLabs.Saving;
using Unity.Entities;
using UnityEngine.Serialization;

[Serializable]
[Save]
public struct Health : IComponentData
{
    public int InitHealth;
    public int Value;
    
    public Health(int health)
    {
        InitHealth = health;
        Value = health;
    }
}

[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
public partial struct HealthSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        new Job(){ ecb = delayedEcb }.Schedule();
    }
    
    [WithDisabled(typeof(DestroyFlag))]
    [WithNone(typeof(SurvivorTag))]
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer ecb;
    
        public void Execute(Entity entity, in Health health, EnabledRefRW<DestroyFlag> destroyFlag)
        {
            if (health.Value <= 0)
            {
                destroyFlag.ValueRW = true;
            }
        }
    }
}