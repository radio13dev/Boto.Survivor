using BovineLabs.Saving;
using Unity.Entities;
using UnityEngine;
using Random = Unity.Mathematics.Random;

/// <summary>
/// Holds all the singleton data for the running game:
///     StepController, SharedRandom, EnemySpawningEnabled, etc...
/// </summary>
[Save]
public struct GameSingleton : IComponentData { }

[Save]
public struct StepController : IComponentData
{
    public long Step;
}

[Save]
public struct SharedRandom : IComponentData
{
    public Random Random;
}

[Save]
public struct EnemySpawningEnabled : IComponentData { }


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

public class GameSingletonAuthoring : MonoBehaviour
{
    public bool EnemySpawningEnabled;

    partial class Baker : Baker<GameSingletonAuthoring>
    {
        public override void Bake(GameSingletonAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            
            AddComponent<GameSingleton>(entity);
            AddComponent<StepController>(entity);
            AddComponent<SharedRandom>(entity);
            
            if (authoring.EnemySpawningEnabled)
                AddComponent<EnemySpawningEnabled>(entity);
        }
    }
}