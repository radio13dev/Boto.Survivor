using System;
using System.Collections.Generic;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[Save]
public struct ItemDropOnDestroy : IBufferElementData
{
    public Drop.DropType Drop;
    public int Chance;
    public int MinCount;
    public int MaxCount;
}

public class ItemDropOnDestroyAuthoring : MonoBehaviour
{
    public List<Drop> Drops = new();

    public class Baker : Baker<ItemDropOnDestroyAuthoring>
    {
        public override void Bake(ItemDropOnDestroyAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            var drops = AddBuffer<ItemDropOnDestroy>(entity);
            foreach (var drop in authoring.Drops)
            {
                drops.Add(new ItemDropOnDestroy()
                {
                    Drop = drop.Type,
                    Chance = drop.Chance,
                    MinCount = drop.MinCount,
                    MaxCount = drop.MaxCount
                });
            }
        }
    }
}

[Serializable]
public class Drop
{
    public enum DropType
    {
        Gem,
        Ring
    }

    public DropType Type;
    [Range(0, 1000)] public int Chance;
    public int MinCount;
    public int MaxCount;
}

[UpdateBefore(typeof(DestroySystem))]
[UpdateInGroup(typeof(DestroySystemGroup))]
public partial struct ItemDropOnDestroySystem : ISystem
{
    EntityQuery m_CleanupQuery;
    EntityQuery m_PlayerQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameManager.Resources>();
        state.RequireForUpdate<SharedRandom>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        m_CleanupQuery = SystemAPI.QueryBuilder().WithAll<ItemDropOnDestroy, DestroyFlag>().Build();
        state.RequireForUpdate(m_CleanupQuery);
        m_PlayerQuery = SystemAPI.QueryBuilder().WithAll<PlayerControlledSaveable>().Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var players = m_PlayerQuery.ToComponentDataArray<PlayerControlledSaveable>(Allocator.TempJob);
        state.Dependency = new Job()
        {
            ecb = delayedEcb,
            baseRandom = SystemAPI.GetSingleton<SharedRandom>().Random,
            movementLookup = SystemAPI.GetComponentLookup<Movement>(true),
            GemDropTemplate = SystemAPI.GetSingleton<GameManager.Resources>().GemDropTemplate,
            RingDropTemplates = SystemAPI.GetSingletonBuffer<GameManager.RingDropTemplate>(true),
            GemVisuals = SystemAPI.GetSingletonBuffer<GameManager.GemVisual>(true),
            PlayerControlled = players
        }.Schedule(state.Dependency);
        players.Dispose(state.Dependency);
    }

    [WithAll(typeof(DestroyFlag))]
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer ecb;
        [ReadOnly] public Random baseRandom;
        [ReadOnly] public ComponentLookup<Movement> movementLookup;
        [ReadOnly] public Entity GemDropTemplate;
        [ReadOnly] public DynamicBuffer<GameManager.RingDropTemplate> RingDropTemplates;
        [ReadOnly] public DynamicBuffer<GameManager.GemVisual> GemVisuals;
        [ReadOnly] public NativeArray<PlayerControlledSaveable> PlayerControlled;

        public void Execute(Entity entity, in DynamicBuffer<ItemDropOnDestroy> items, in LocalTransform transform)
        {
            var random = baseRandom;
            for (int i = 0; i < items.Length; i++)
            {
                if (random.NextInt(1000) >= items[i].Chance) continue;

                var count = random.NextInt(items[i].MinCount, items[i].MaxCount + 1);
                Entity newDropE;
                switch (items[i].Drop)
                {
                    case Drop.DropType.Gem:
                        for (int loop = 0; loop < count; loop++)
                        {
                            newDropE = ecb.Instantiate(GemDropTemplate);
                            var gem = Gem.Generate(ref random);
                            Gem.SetupEntity(newDropE, PlayerControlled[random.NextInt(PlayerControlled.Length)].Index, ref random, ref ecb, transform, movementLookup[entity], gem,
                                GemVisuals);
                        }
                        break;

                    case Drop.DropType.Ring:
                        for (int loop = 0; loop < count; loop++)
                        {
                            var ring = RingStats.Generate(ref random);
                            newDropE = ecb.Instantiate(RingDropTemplates[ring.Tier].Entity);
                            Ring.SetupEntity(newDropE, PlayerControlled[random.NextInt(PlayerControlled.Length)].Index, ref random, ref ecb, transform, movementLookup[entity],
                                ring);
                        }
                        break;
                        
                    default:
                        Debug.LogError($"Unknown drop type {items[i].Drop} on entity {entity}");
                        break;
                }
            }
        }
    }
}