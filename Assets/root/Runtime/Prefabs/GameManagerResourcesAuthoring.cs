using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

public struct GameManager : IComponentData
{
    [ChunkSerializable]
    public struct Resources : IComponentData
    {
        public Entity ProjectileTemplate;
        public Entity SurvivorTemplate;
        
        public Entity Projectile_Survivor_Laser;
    }
    
    public struct SpecificPrefabs : IBufferElementData
    {
        public UnityObjectRef<GameObject> Prefab;
    }
    
    [ChunkSerializable]
    public struct SceneLoader : IComponentData
    {
        public EntitySceneReference GameManagerSubscene;
        public EntitySceneReference GameSubscene;
    }
}

public class GameManagerResourcesAuthoring : MonoBehaviour
{
    public GameObject ProjectileTemplate;
    public GameObject SurvivorTemplate;
    public GameObject Projectile_Survivor_Laser;
    
    public SpecificPrefabDatabase SpecificPrefabDatabase;

    public class Baker : Baker<GameManagerResourcesAuthoring>
    {
        public override void Bake(GameManagerResourcesAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new GameManager());
            AddComponent(entity, new GameManager.Resources()
            {
                ProjectileTemplate = GetEntity(authoring.ProjectileTemplate, TransformUsageFlags.WorldSpace),
                SurvivorTemplate = GetEntity(authoring.SurvivorTemplate, TransformUsageFlags.WorldSpace),
                Projectile_Survivor_Laser = GetEntity(authoring.Projectile_Survivor_Laser, TransformUsageFlags.WorldSpace)
            });
            
            if (authoring.SpecificPrefabDatabase)
            {
                var buffer = AddBuffer<GameManager.SpecificPrefabs>(entity);
                for (int i = 0; i < authoring.SpecificPrefabDatabase.Prefabs.Count; i++)
                    buffer.Add(new GameManager.SpecificPrefabs() { Prefab = authoring.SpecificPrefabDatabase.Prefabs[i] });
            }
        }
    }
}

[RequireMatchingQueriesForUpdate]
public partial struct SpecificPrefabDatabaseCleanup : ISystem
{
    EntityQuery m_CleanupQuery;

    public void OnCreate(ref SystemState state)
    {
        m_CleanupQuery = SystemAPI.QueryBuilder().WithAll<GameManager.SpecificPrefabs>().WithNone<GameManager>().Build();
        state.RequireForUpdate(m_CleanupQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        state.EntityManager.DestroyEntity(m_CleanupQuery);
    }
}