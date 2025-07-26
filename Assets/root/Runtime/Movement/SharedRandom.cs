using BovineLabs.Saving;
using Unity.Entities;
using Random = Unity.Mathematics.Random;

[Save]
public struct SharedRandom : IComponentData
{
    public Random Random;
}

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