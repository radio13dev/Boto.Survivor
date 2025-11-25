using System;
using BovineLabs.Saving;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class ChallengeShrineAuthoring : MonoBehaviour
{
    public DatabaseRef<GameObject, GenericDatabase> ToSpawn = new();
    class Baker : Baker<ChallengeShrineAuthoring>
    {
        public override void Bake(ChallengeShrineAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new ChallengeShrine()
            {
                TerrainGroupToSpawn = authoring.ToSpawn.GetAssetIndex()
            });
        }
    }
}

[Save]
[Serializable]
public struct ChallengeShrine : IComponentData
{
    public int TerrainGroupToSpawn;
}

public partial struct ChallengeShrineSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (shrine, circlable, transform, e) in SystemAPI.Query<RefRO<ChallengeShrine>, RefRO<Circlable>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            if (circlable.ValueRO.Charge >= circlable.ValueRO.MaxCharge)
            {
                // Destroy this
                SystemAPI.SetComponentEnabled<DestroyFlag>(e, true);
                
                // Spawn the challenge
                var toSpawn = SystemAPI.GetSingletonBuffer<GameManager.Prefabs>()[shrine.ValueRO.TerrainGroupToSpawn];
                var spawned = TerrainGroupInitSystem.SpawnTerrainGroup(state.EntityManager, toSpawn.Entity, transform.ValueRO);
                SystemAPI.SetComponent(spawned, new DestroyAtTime()
                {
                    DestroyTime = SystemAPI.Time.ElapsedTime + 60
                });
            }
        }
    }
}