using System.Collections.Generic;
using BovineLabs.Saving;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public struct PlayerControlled : ISharedComponentData
{
    public int Index;
}

[Save]
public struct PlayerControlledSaveable : IComponentData
{
    public int Index;
}

public class SurvivorAuthoring : MonoBehaviour
{
    public Health Health = new Health(100);
    public RingStats[] InitialRings = new RingStats[8];
    public List<Gem> InitialGems = new();

    partial class Baker : Baker<SurvivorAuthoring>
    {
        public override void Bake(SurvivorAuthoring authoring)
        {
            if (!DependsOn(GetComponent<StepInputAuthoring>()))
            {
                Debug.LogError($"Cannot bake {authoring.gameObject}. Requires component: {nameof(StepInputAuthoring)}");
                return;
            }
            
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            
            // Basic character setup
            AddComponent(entity, new PlayerControlledSaveable(){ Index = -1 });
            AddComponent(entity, new CharacterTag());
            AddComponent(entity, new SurvivorTag());
            AddComponent(entity, authoring.Health);
            
            AddComponent<EnemySpawner>(entity);
            
            // Abilities and Input Lockouts
            AddComponent(entity, new MovementInputLockout());
            SetComponentEnabled<MovementInputLockout>(entity, false);
            AddComponent(entity, new ActiveLockout());
            SetComponentEnabled<ActiveLockout>(entity, false);
            AddComponent(entity, new RollActive());
            SetComponentEnabled<RollActive>(entity, false);
            
            // Stats
            AddComponent(entity, new CompiledStats());
            AddComponent(entity, new CompiledStatsDirty());
            
            // Rings and inventory
            var rings = AddBuffer<Ring>(entity);
            rings.Resize(Ring.k_RingCount, NativeArrayOptions.ClearMemory);
            var equippedGems = AddBuffer<EquippedGem>(entity);
            equippedGems.Resize(Ring.k_RingCount*Gem.k_GemsPerRing, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < rings.Length && i < authoring.InitialRings.Length; i++)
                rings[i] = new Ring(){ Stats = authoring.InitialRings[i] };
            
            var inventoryGems = AddBuffer<InventoryGem>(entity);
            foreach (var initGem in authoring.InitialGems)
                inventoryGems.Add(new InventoryGem(initGem));
            
            // Loop triggers
            AddBuffer<ProjectileLoopTriggerQueue>(entity);
            AddBuffer<OwnedProjectiles>(entity);
        }
    }
}

/// <summary>
/// Convenient link between player ID and the entity that controls it.
/// </summary>
/// <remarks>
/// Entity values are different between games, so this is the only way to reference a specific player.
/// </remarks>
public struct PlayerControlledLink : IBufferElementData
{
    public Entity Value;

    public PlayerControlledLink(Entity value)
    {
        this.Value = value;
    }
}

[UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
public partial struct PlayerControlledSystem : ISystem
{
    EntityQuery m_Query;
    public void OnCreate(ref SystemState state)
    {
        state.EntityManager.CreateSingletonBuffer<PlayerControlledLink>();
        m_Query = SystemAPI.QueryBuilder().WithAll<PlayerControlledSaveable>().WithNone<PlayerControlled>().Build();
        state.RequireForUpdate(m_Query);
    }

    public void OnUpdate(ref SystemState state)
    {
        using var entities = m_Query.ToEntityArray(Allocator.Temp);
        using var ids = m_Query.ToComponentDataArray<PlayerControlledSaveable>(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            state.EntityManager.AddSharedComponent(entities[i], new PlayerControlled(){ Index = ids[i].Index });
            
            var playerControlledLink = SystemAPI.GetSingletonBuffer<PlayerControlledLink>();
            if (playerControlledLink.Length <= ids[i].Index)
                playerControlledLink.Resize(ids[i].Index + 1, NativeArrayOptions.ClearMemory);
            playerControlledLink[ids[i].Index] = new PlayerControlledLink(entities[i]);
            Debug.Log($"Added playerid {ids[i].Index} to entity {entities[i]}");
        }
    }
}