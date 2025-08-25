using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class RingPickupAuthoring : MonoBehaviour
{
    [Header("Set empty to generate random ring")]
    public RingStats SpecificRing;
    [Header("Set to a value to have this (and matching values) be destroyed on item collect.")]
    public int LootKey = 0;
    
    partial class Baker : Baker<RingPickupAuthoring>
    {
        public override void Bake(RingPickupAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent<Interactable>(entity);
            
            AddSharedComponent<LootKey>(entity, new LootKey(){ Value = authoring.LootKey });
            
            AddComponent<RingStats>(entity, authoring.SpecificRing);
            if (authoring.SpecificRing.PrimaryEffect == RingPrimaryEffect.None)
            {
                AddComponent<LootGenerationRequest>(entity);
                SetComponentEnabled<LootGenerationRequest>(entity, true);
            }
        }
    }
}

[Save]
public struct LootKey : ISharedComponentData
{
    public int Value;
}

[Save]
public struct LootGenerationRequest : IComponentData, IEnableableComponent{}

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
public partial struct LootGenerationRequestSystem : ISystem
{
    EntityQuery m_Query;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SharedRandom>();
        m_Query = SystemAPI.QueryBuilder().WithAll<LootGenerationRequest, RingStats, LocalTransform>().Build();
        state.RequireForUpdate(m_Query);
    }

    public void OnUpdate(ref SystemState state)
    {
        var r = SystemAPI.GetSingleton<SharedRandom>().Random;
        
        using var entities = m_Query.ToEntityArray(Allocator.Temp);
        using var transforms = m_Query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        
        NativeArray<int> correctOrdering = new NativeArray<int>(transforms.Length, Allocator.Temp);
        for (int i = 0; i < correctOrdering.Length; i++) correctOrdering[i] = i; // Array of initial indexes
        correctOrdering.Sort(new LocalTransformComparer(transforms)); // Sort to change it into the order of indexes to process (should be consistent across platform)
        for (int i = 0; i < correctOrdering.Length; i++)
        {
            var ringE = entities[correctOrdering[i]];
            SystemAPI.SetComponent(ringE, RingStats.Generate(ref r));
            SystemAPI.SetComponentEnabled<LootGenerationRequest>(ringE, false);
            GameEvents.Trigger(GameEvents.Type.VisualsUpdated, ringE);
        }
        correctOrdering.Dispose();
    }
}