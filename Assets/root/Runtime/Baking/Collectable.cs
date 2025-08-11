using System.Collections.Generic;
using BovineLabs.Saving;
using Collisions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Collider = Collisions.Collider;

[Save]
public struct Collectable : IComponentData, IEnableableComponent
{
    public Entity CollectedBy;
}

[Save]
public struct Collected : IComponentData, IEnableableComponent{}

[Save]
public struct CollectCollider : IComponentData
{
    public Collider Collider;
}

[UpdateAfter(typeof(CollisionSystemGroup))]
[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
public partial class CollectableSystemGroup : ComponentSystemGroup
{
}

/// <summary>
/// Destroys collectables when collected.
/// </summary>
[UpdateInGroup(typeof(CollectableSystemGroup), OrderLast = true)]
public partial struct CollectableClearSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        state.Dependency = new Job()
        {
            dt = SystemAPI.Time.DeltaTime,
            ecb = delayedEcb.AsParallelWriter(),
            m_TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true)
        }.ScheduleParallel(state.Dependency);
    }
    
    [WithAll(typeof(Collectable))]
    [WithPresent(typeof(Collected), typeof(Grounded))]
    [WithAbsent(typeof(DestroyFlag))]
    partial struct Job : IJobEntity
    {
        public const float k_MaxCollectableSpeed = 160;
        public const float k_CollectRadius = 0.4f;
        
        [ReadOnly] public float dt;
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public ComponentLookup<LocalTransform> m_TransformLookup;
    
        public void Execute([ChunkIndexInQuery] int key, Entity collectableE, in Collectable collectable, 
            EnabledRefRW<Collected> collected, EnabledRefRW<Grounded> grounded,
            in LocalTransform collectableT, ref Force force, in Movement movement)
        {
            if (collected.ValueRO)
            {
                // We let this entity live for 1 frame so systems can execute on it
                ecb.AddComponent<DestroyFlag>(key, collectableE);
                return;
            }
            
            if (!m_TransformLookup.TryGetComponent(collectable.CollectedBy, out var target))
            {
                Debug.LogWarning($"Couldn't find collection target, destroying...");
                ecb.AddComponent<DestroyFlag>(key, collectableE);
                return;
            }
            
            var dif = target.Position - collectableT.Position;
            var difLen = math.lengthsq(dif);
            if (difLen < k_CollectRadius)
            {
                collected.ValueRW = true;
                return;
            }
            
            var dir = math.normalizesafe(dif);
            force.Velocity -= movement.Velocity*dt;
            force.Velocity += dir*k_MaxCollectableSpeed*dt;
            grounded.ValueRW = false;
        }
    }
}

[UpdateInGroup(typeof(CollectableSystemGroup))]
public partial struct GemCollectableSystem : ISystem
{
    EntityQuery m_Query;

    public void OnCreate(ref SystemState state)
    {
        m_Query = SystemAPI.QueryBuilder().WithAll<Collectable, GemDrop, Collected>().Build();
        state.RequireForUpdate(m_Query);
    }

    public void OnUpdate(ref SystemState state)
    {
        using var drops = m_Query.ToComponentDataArray<GemDrop>(Allocator.Temp);
        using var collectedBy = m_Query.ToComponentDataArray<Collectable>(Allocator.Temp);
        
        // Gotta do this based on the Hash ordering of the gem drops
        NativeArray<int> correctOrdering = new NativeArray<int>(drops.Length, Allocator.Temp);
        for (int i = 0; i < correctOrdering.Length; i++) correctOrdering[i] = i; // Array of initial indexes
        correctOrdering.Sort(new GemComparer(drops.Reinterpret<Gem>())); // Sort to change it into the order of indexes to process (should be consistent across platform)
        
        for (int i = 0; i < correctOrdering.Length; i++)
        {
            // Give currency to the entity
            int index = correctOrdering[i];
            if (SystemAPI.HasBuffer<InventoryGem>(collectedBy[index].CollectedBy))
            {
                var inventoryRW = SystemAPI.GetBuffer<InventoryGem>(collectedBy[index].CollectedBy);
                inventoryRW.Add(new InventoryGem(drops[index].Gem));
            }
        }
        
        correctOrdering.Dispose();
    }
}

public struct GemComparer : IComparer<int>
{
    NativeArray<Gem> m_Gems;
    
    public GemComparer(NativeArray<Gem> gems) => m_Gems = gems;

    public int Compare(int x, int y)
    {
        return m_Gems[x].CompareTo(m_Gems[y]);
    }
}