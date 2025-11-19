using System.Collections;
using System.Diagnostics.Contracts;
using Unity.Entities;
using UnityEngine;
using Random = Unity.Mathematics.Random;

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
        }
    }
}

public struct LootDropInteractable : IComponentData, IEnableableComponent
{
    public Random Random;

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
            lootDrop = new LootDropInteractable()
            {
                Random = Random.Random
            };
            lootDropEnabledState.ValueRW = true;
        }
    }
}
