using System;
using BovineLabs.Saving;
using Collisions;
using NativeTrees;
using Unity.Entities;

[Save]
public struct Collectable : IComponentData, IEnableableComponent
{
    public Entity CollectedBy;
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
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        foreach (var collectable in SystemAPI.Query<RefRO<Collectable>>().WithEntityAccess())
        {
            delayedEcb.DestroyEntity(collectable.Item2);
        }
    }
}