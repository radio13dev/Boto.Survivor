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
                    Drop =  drop.Type,
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
        Gem
    }
    public DropType Type;
    [Range(0,100)]
    public int Chance;
    public int MinCount;
    public int MaxCount;
}

[UpdateBefore(typeof(DestroySystem))]
[UpdateInGroup(typeof(DestroySystemGroup))]
public partial struct ItemDropOnDestroySystem : ISystem
{
    EntityQuery m_CleanupQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameManager.Resources>();
        state.RequireForUpdate<SharedRandom>();
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        m_CleanupQuery = SystemAPI.QueryBuilder().WithAll<ItemDropOnDestroy, DestroyFlag>().Build();
        state.RequireForUpdate(m_CleanupQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        state.Dependency = new Job()
        {
            ecb = delayedEcb,
            baseRandom = SystemAPI.GetSingleton<SharedRandom>().Random,
            rotationalInertiaLookup = SystemAPI.GetComponentLookup<RotationalInertia>(true),
            movementLookup = SystemAPI.GetComponentLookup<Movement>(true),
            GemDropTemplate = SystemAPI.GetSingleton<GameManager.Resources>().GemDropTemplate
        }.Schedule(state.Dependency);
    }
    
    [WithAll(typeof(DestroyFlag))]
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer ecb;
        [ReadOnly] public Random baseRandom;
        [ReadOnly] public ComponentLookup<RotationalInertia> rotationalInertiaLookup;
        [ReadOnly] public ComponentLookup<Movement> movementLookup;
        [ReadOnly] public Entity GemDropTemplate;
        [ReadOnly] public DynamicBuffer<GameManager.GemVisual> GemVisuals;
    
        public void Execute(Entity entity, in DynamicBuffer<ItemDropOnDestroy> items, in LocalTransform transform)
        {
            var random = baseRandom;
            for (int i = 0; i < items.Length; i++)
            {
                if (random.NextInt(100) >= items[i].Chance) continue;
                
                var count = random.NextInt(items[i].MinCount, items[i].MaxCount + 1);
                for (int loop = 0; loop < count; loop++)
                {
                    Entity newDropE;
                    switch (items[i].Drop)
                    {
                        case Drop.DropType.Gem:
                            newDropE = ecb.Instantiate(GemDropTemplate);
                            var gem = Gem.Generate(ref random);
                            ecb.SetComponent(newDropE, new GemDrop()
                            {
                                Gem = gem
                            });
                            ecb.SetSharedComponent(newDropE, new InstancedResourceRequest(GemVisuals[(int)gem.GemType].InstancedResourceIndex));
                            break;
                        default:
                            Debug.LogError($"Unknown drop type {items[i].Drop} on entity {entity}");
                            continue;
                    }
                    ecb.SetComponent(newDropE, transform);
                
                    if (rotationalInertiaLookup.HasComponent(GemDropTemplate))
                    {
                        var newDropInertia = new RotationalInertia();
                        newDropInertia.Set(random.NextFloat3(), 1);
                        ecb.SetComponent(newDropE, newDropInertia);
                    }
                
                    if (movementLookup.HasComponent(GemDropTemplate) && movementLookup.TryGetComponent(entity, out var movement))
                    {
                        var newDropMovement = new Movement();
                        newDropMovement.Velocity = movement.Velocity + (math.length(movement.Velocity)/2 + 1) * random.NextFloat3();
                        ecb.SetComponent(newDropE, newDropMovement);
                    }
                }
            }
        }
    }
}