using System;
using AYellowpaper.SerializedCollections;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public struct GameManager : IComponentData
{
    [ChunkSerializable]
    public struct Resources : IComponentData
    {
        public Entity GemDropTemplate;
        public Entity RingDropTemplate;
    }
    
    public struct SpecificPrefabs : IBufferElementData
    {
        public UnityObjectRef<GameObject> Prefab;
    }
    
    public struct InstancedResources : IBufferElementData
    {
        public UnityObjectRef<InstancedResource> Instance;
        public SpriteAnimData AnimData;
        public bool Valid;

        public void Validate()
        {
            if (!Instance.Value || !Instance.Value.Mesh || !Instance.Value.Material)
            {
                Debug.Log($"{Instance.Value} invalid. Mesh or Mat null: {Instance.Value.Mesh}, {Instance.Value.Material}");
                Valid = false;
            }
            else
                Valid = true;
        }
    }
    
    [ChunkSerializable]
    public struct SceneLoader : IComponentData
    {
        public EntitySceneReference GameManagerSubscene;
        public EntitySceneReference GameSubscene;
    }
    
    public struct Particles : IBufferElementData
    {
        public UnityObjectRef<PooledParticle> Prefab;
    }
    
    public struct Terrain : IBufferElementData
    {
        public Entity Entity;
    }
    
    public struct Projectiles : IBufferElementData
    {
        public Entity Entity;
    }
    
    public struct Enemies : IBufferElementData
    {
        public Entity Entity;
        public int Chance;
    }
    
    public struct GemVisual : IBufferElementData
    {
        public int InstancedResourceIndex;
    }
    public struct RingVisual : IBufferElementData
    {
        public int InstancedResourceIndex;
    }
    
    public struct TerrainGroup : IBufferElementData
    {
        public Entity Entity;
    }
    
    public struct Survivors : IBufferElementData
    {
        public Entity Entity;
    }
}


public class GameManagerResourcesAuthoring : MonoBehaviour
{
    public GemDropAuthoring GemDropTemplate;
    public RingDropAuthoring RingDropTemplate;
    
    public SpecificPrefabDatabase SpecificPrefabDatabase;
    public InstancedResourcesDatabase InstancedResourcesDatabase;
    public ParticleDatabase ParticleDatabase;
    public TerrainAuthoring[] Terrains;
    public ProjectileAuthoring[] Projectiles;
    public EnemyCharacterAuthoring[] Enemies;
    public SerializedDictionary<Gem.Type, InstancedResource> GemVisuals;
    public SerializedDictionary<RingPrimaryEffect, InstancedResource> RingVisuals;
    public TerrainGroupAuthoring[] TerrainGroups;
    public SurvivorAuthoring[] Survivors;

    public class Baker : Baker<GameManagerResourcesAuthoring>
    {
        public override void Bake(GameManagerResourcesAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new GameManager());
            AddComponent(entity, new GameManager.Resources()
            {
                GemDropTemplate = GetEntity(authoring.GemDropTemplate, TransformUsageFlags.WorldSpace),
                RingDropTemplate = GetEntity(authoring.RingDropTemplate, TransformUsageFlags.WorldSpace),
            });
            
            if (authoring.SpecificPrefabDatabase)
            {
                var buffer = AddBuffer<GameManager.SpecificPrefabs>(entity);
                for (int i = 0; i < authoring.SpecificPrefabDatabase.Prefabs.Count; i++)
                    buffer.Add(new GameManager.SpecificPrefabs() { Prefab = authoring.SpecificPrefabDatabase.Prefabs[i] });
            }
            
            if (authoring.InstancedResourcesDatabase)
            {
                var buffer = AddBuffer<GameManager.InstancedResources>(entity);
                for (int i = 0; i < authoring.InstancedResourcesDatabase.Assets.Count; i++)
                {
                    var instance = authoring.InstancedResourcesDatabase.Assets[i];
                    var instancedResource = new GameManager.InstancedResources() { Instance = instance, AnimData = instance.AnimData };
                    instancedResource.Validate();
                    buffer.Add(instancedResource);
                }
            }
            
            if (authoring.ParticleDatabase)
            {
                var buffer = AddBuffer<GameManager.Particles>(entity);
                for (int i = 0; i < authoring.ParticleDatabase.Length; i++)
                {
                    buffer.Add(new GameManager.Particles(){ Prefab = authoring.ParticleDatabase[i] });
                }
            }
            
            if (authoring.Terrains?.Length > 0)
            {
                var buffer = AddBuffer<GameManager.Terrain>(entity);
                for (int i = 0; i < authoring.Terrains.Length; i++)
                {
                    buffer.Add(new GameManager.Terrain(){ Entity = GetEntity(authoring.Terrains[i].gameObject, TransformUsageFlags.WorldSpace) });
                }
            }
            
            if (authoring.Projectiles?.Length > 0)
            {
                var buffer = AddBuffer<GameManager.Projectiles>(entity);
                for (int i = 0; i < authoring.Projectiles.Length; i++)
                {
                    buffer.Add(new GameManager.Projectiles(){ Entity = GetEntity(authoring.Projectiles[i].gameObject, TransformUsageFlags.WorldSpace) });
                }
            }
            
            if (authoring.Enemies?.Length > 0)
            {
                var buffer = AddBuffer<GameManager.Enemies>(entity);
                for (int i = 0; i < authoring.Enemies.Length; i++)
                {
                    buffer.Add(new GameManager.Enemies(){ Entity = GetEntity(authoring.Enemies[i].gameObject, TransformUsageFlags.WorldSpace), Chance = authoring.Enemies[i].Chance });
                }
            }
            
            if (authoring.GemVisuals?.Count > 0)
            {
                if (!authoring.InstancedResourcesDatabase)
                {
                    Debug.LogError($"Couldn't author GemVisuals, instanced resource database null.");
                }
                else
                {
                    var buffer = AddBuffer<GameManager.GemVisual>(entity);
                    buffer.Resize(Enum.GetValues(typeof(Gem.Type)).Length , NativeArrayOptions.ClearMemory);
                    foreach (var kvp in authoring.GemVisuals)
                    {
                        var index = authoring.InstancedResourcesDatabase.Assets.IndexOf(kvp.Value);
                        if (index == -1)
                        {
                            Debug.LogError($"{kvp.Value} cannot be found in the InstancedResourcesDatabase");
                        }
                        buffer[(int)kvp.Key] = new GameManager.GemVisual(){ InstancedResourceIndex = index } ;
                    }
                }
            }
            
            if (authoring.RingVisuals?.Count > 0)
            {
                if (!authoring.InstancedResourcesDatabase)
                {
                    Debug.LogError($"Couldn't author RingVisuals, instanced resource database null.");
                }
                else
                {
                    var buffer = AddBuffer<GameManager.RingVisual>(entity);
                    buffer.Resize(Enum.GetValues(typeof(RingPrimaryEffect)).Length , NativeArrayOptions.ClearMemory);
                    foreach (var kvp in authoring.RingVisuals)
                    {
                        var index = authoring.InstancedResourcesDatabase.Assets.IndexOf(kvp.Value);
                        if (index == -1)
                        {
                            Debug.LogError($"{kvp.Value} cannot be found in the InstancedResourcesDatabase");
                        }
                        buffer[(int)kvp.Key] = new GameManager.RingVisual(){ InstancedResourceIndex = index } ;
                    }
                }
            }
            
            if (authoring.TerrainGroups?.Length > 0)
            {
                var buffer = AddBuffer<GameManager.TerrainGroup>(entity);
                for (int i = 0; i < authoring.TerrainGroups.Length; i++)
                {
                    buffer.Add(new GameManager.TerrainGroup(){ Entity = GetEntity(authoring.TerrainGroups[i].gameObject, TransformUsageFlags.WorldSpace) });
                }
            }
            
            if (authoring.Survivors?.Length > 0)
            {
                var buffer = AddBuffer<GameManager.Survivors>(entity);
                for (int i = 0; i < authoring.Survivors.Length; i++)
                {
                    buffer.Add(new GameManager.Survivors(){ Entity = GetEntity(authoring.Survivors[i].gameObject, TransformUsageFlags.WorldSpace) });
                }
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