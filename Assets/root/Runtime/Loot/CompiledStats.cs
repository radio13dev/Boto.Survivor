using BovineLabs.Saving;
using Unity.Burst;
using Unity.Entities;

[Save]
public struct CompiledStats : IComponentData
{
    public float ProjectileRate;
    public float ProjectileSpeed;
    public float ProjectileDuration;
    public float ProjectileSize;
    public float ProjectileDamage;
}

[Save]
public struct CompiledStatsDirty : IComponentData, IEnableableComponent
{
}

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
[BurstCompile]
public partial struct CompiledStatsSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CompiledStatsDirty>();
    }

    public void OnUpdate(ref SystemState state)
    {
        new Job().Schedule();
    }
    
    [RequireMatchingQueriesForUpdate]
    [WithAll(typeof(CompiledStatsDirty))]
    [BurstCompile]
    partial struct Job : IJobEntity
    {
        public void Execute(Entity entity, ref CompiledStats stats, EnabledRefRW<CompiledStatsDirty> statsDirty, in DynamicBuffer<Ring> rings, in DynamicBuffer<EquippedGem> gems, ref DynamicBuffer<OwnedProjectiles> ownedProjectiles)
        {
            stats = new();
            //for (int i = 0; i < rings.Length; i++)
            //    stats.Add(rings[i].Stats);
            
            // Destroy any owned projectiles that are no longer valid
            
            
            statsDirty.ValueRW = false;
            GameEvents.Trigger(GameEvents.Type.InventoryChanged, entity);
        }
    }
}