using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
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
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SharedRandom>();
    }

    public void OnUpdate(ref SystemState state)
    {
        new Job()
        {
            SharedRandom = SystemAPI.GetSingleton<SharedRandom>()
        }.Schedule();
    }
    
    [WithAll(typeof(LootGenerationRequest))]
    partial struct Job : IJobEntity
    {
        [ReadOnly] public SharedRandom SharedRandom;
    
        public void Execute(Entity ringE, EnabledRefRW<LootGenerationRequest> requestState, ref RingStats ringStats)
        {
            var r = SharedRandom.Random;
            ringStats = RingStats.Generate(ref r);
            requestState.ValueRW = false;
            GameEvents.Trigger(GameEvents.Type.VisualsUpdated, ringE);
        }
    }
}