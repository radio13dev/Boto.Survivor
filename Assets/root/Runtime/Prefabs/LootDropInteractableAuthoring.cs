using System.Collections;
using System.Diagnostics.Contracts;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public enum eLootTier : byte
{
    Common,
    Rare,
    Epic,
    Legendary
}

public static class LootTier
{
    [Pure]
    public static eLootTier Generate(ref Random random)
    {
        return (eLootTier)random.NextInt(4);
    }
}

public class LootDropInteractableAuthoring : MonoBehaviour
{
    partial class Baker : Baker<LootDropInteractableAuthoring>
    {
        public override void Bake(LootDropInteractableAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent<Interactable>(entity);
            AddComponent<LootDropInteractable>(entity);
            SetComponentEnabled<LootDropInteractable>(entity, false);
            AddSharedComponent(entity, new LootKey());
        }
    }
}

public readonly struct LootDropInteractable : IComponentData, IEnableableComponent
{
    public readonly eLootTier Tier; // 0, 1, 2 or 3
    public readonly Random Random;

    public LootDropInteractable(eLootTier tier, Random random)
    {
        Tier = tier;
        Random = random;
    }

    [Pure]
    public Option[] GetOptions()
    {
        var r = Random;
        var options = new Option[3];
        for (int i = 0; i < options.Length; i++)
        {
            options[i] = new Option()
            {
                Ring = new Ring()
                {
                    Stats = RingStats.Generate(ref r)
                }
            };
        }
        
        return options;
    }
    
    public struct Option
    {
        public Ring Ring;
    }

    public static LootDropInteractable Generate(ref Random random)
    {
        return new LootDropInteractable(LootTier.Generate(ref random), random);
    }

    public static void SetupEntity(Entity newDropE, byte playerId, ref Random random, ref EntityCommandBuffer ecb, in LocalTransform transform, LootDropInteractable loot)
    {
        var adjustedT = transform;
        adjustedT.Scale = 1;
        adjustedT.Position = TorusMapper.SnapToSurface(transform.Position) + random.NextFloat3();
        ecb.SetComponent(newDropE, adjustedT);
        ecb.SetComponent(newDropE, loot);
        ecb.SetComponentEnabled<LootDropInteractable>(newDropE, true);
        ecb.SetComponent(newDropE, new Collectable()
        {
            PlayerId = playerId
        });
        // CLIENTSIDE ONLY
        if (Game.ClientPlayerIndex.Data != -1 && Game.ClientPlayerIndex.Data != playerId)
        {
            ecb.AddComponent(newDropE, new Hidden());
        }
    }
}

public partial struct LootDropInteractableSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SharedRandom>();
    }

    public void OnUpdate(ref SystemState state)
    {
        new Job()
        {
            Random = SystemAPI.GetSingleton<SharedRandom>()
        }.Schedule();
    }
    
    [WithDisabled(typeof(LootDropInteractable))]
    partial struct Job : IJobEntity
    {
        public SharedRandom Random;
    
        public void Execute(ref LootDropInteractable lootDrop, EnabledRefRW<LootDropInteractable> lootDropEnabledState)
        {
            var r = Random.Random;
            lootDrop = new LootDropInteractable(LootTier.Generate(ref r), r);
            lootDropEnabledState.ValueRW = true;
        }
    }
}
