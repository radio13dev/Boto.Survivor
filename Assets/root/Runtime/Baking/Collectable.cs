using System;
using BovineLabs.Saving;
using Collisions;
using NativeTrees;
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
        [ReadOnly] public float dt;
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public ComponentLookup<LocalTransform> m_TransformLookup;
    
        public void Execute([ChunkIndexInQuery] int key, Entity collectableE, in Collectable collectable, 
            EnabledRefRW<Collected> collected, EnabledRefRW<Grounded> grounded,
            in LocalTransform collectableT, in MovementSettings movementSettings, ref Force force)
        {
            if (collected.ValueRO)
            {
                // We let this entity live for 1 frame so systems can execute on it
                Debug.Log($"Collected.");
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
            if (difLen < movementSettings.CollectRadius)
            {
                collected.ValueRW = true;
                return;
            }
            
            var dir = math.normalizesafe(dif);
            force.Velocity += dir*movementSettings.MaxCollectableSpeed*dt;
            force.Velocity += target.Up()*movementSettings.JumpValue;
            grounded.ValueRW = false;
        }
    }
}