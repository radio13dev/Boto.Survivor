using System;
using BovineLabs.Saving;
using Unity.Entities;

[Serializable]
[Save]
public struct Health : IComponentData
{
    public int Value;
    
    public Health(int health)
    {
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
    
    [WithAbsent(typeof(DestroyFlag))]
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer ecb;
    
        public void Execute(Entity entity, in Health health)
        {
            if (health.Value <= 0)
            {
                ecb.AddComponent<DestroyFlag>(entity);
            }
        }
    }
}