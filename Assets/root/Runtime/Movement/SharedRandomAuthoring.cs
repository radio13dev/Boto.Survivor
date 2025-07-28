using BovineLabs.Saving;
using Unity.Entities;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class SharedRandomAuthoring : MonoBehaviour
{
    partial class Baker : Baker<SharedRandomAuthoring>
    {
        public override void Bake(SharedRandomAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new SharedRandom(){ Random = Random.CreateFromIndex(0) });
        }
    }
}

[Save]
public struct SharedRandom : IComponentData
{
    public Random Random;
}

[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
public partial struct SharedRandomSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SharedRandom>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var random = SystemAPI.GetSingletonRW<SharedRandom>();
        random.ValueRW.Random.NextBool();
    }
}