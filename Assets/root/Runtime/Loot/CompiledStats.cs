using System;
using BovineLabs.Saving;
using Unity.Entities;
using UnityEngine;

[Save]
public struct CompiledStats : IComponentData
{
    public RingStats CombinedRingStats;

    public void Add(RingStats stats)
    {
        CombinedRingStats.Add(stats);
    }
}

[Save]
public struct CompiledStatsDirty : IComponentData, IEnableableComponent
{
}

[RequireMatchingQueriesForUpdate]
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
    partial struct Job : IJobEntity
    {
        public void Execute(Entity entity, ref CompiledStats stats, EnabledRefRW<CompiledStatsDirty> statsDirty, in DynamicBuffer<Ring> rings)
        {
            stats = new();
            for (int i = 0; i < rings.Length; i++)
                stats.Add(rings[i].Stats);
            statsDirty.ValueRW = false;
            Game.SetCacheDirty(entity);
        }
    }
}