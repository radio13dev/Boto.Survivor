using System;
using Collisions;
using NativeTrees;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Collider = Collisions.Collider;

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
    
    /// <summary>
    /// The ULTIMATE generic entity list.
    /// </summary>
    public struct Prefabs : IBufferElementData
    {
        public Entity Entity;

        public static Entity SpawnCircleBlast(in DynamicBuffer<Prefabs> prefabs, ref EntityCommandBuffer ecb, in LocalTransform transform, float radius, double Time, float delay)
        {
            var circleE = ecb.Instantiate(prefabs[1].Entity);
            var t = transform;
            t.Scale = radius;
            ecb.SetComponent(circleE, t);
            ecb.SetComponent(circleE, new SpawnTimeCreated(Time));
            ecb.SetComponent(circleE, new DestroyAtTime(Time + delay));
            return circleE;
        }

        public static Entity SpawnTorusBlast(in DynamicBuffer<Prefabs> prefabs, ref EntityCommandBuffer ecb, in LocalTransform transform, float radiusMin, float radiusMax, double Time, float delay)
        {
            var torusE = ecb.Instantiate(prefabs[2].Entity);
            var t = transform;
            t.Scale = radiusMax;
            ecb.SetComponent(torusE, t);
            ecb.SetComponent(torusE, new TorusMin(radiusMin/radiusMax));
            ecb.SetComponent(torusE, Collider.Torus(radiusMin/radiusMax, 1));
            ecb.SetComponent(torusE, new SpawnTimeCreated(Time));
            ecb.SetComponent(torusE, new DestroyAtTime(Time + delay));
            return torusE;
        }
        
        public static Entity SpawnTorusConeBlast(in DynamicBuffer<Prefabs> prefabs, ref EntityCommandBuffer ecb, in LocalTransform transform, float radiusMin, float radiusMax, float angle, double Time, float delay)
        {
            var torusE = ecb.Instantiate(prefabs[3].Entity);
            var t = transform;
            t.Scale = radiusMax;
            ecb.SetComponent(torusE, t);
            ecb.SetComponent(torusE, new TorusMin(radiusMin/radiusMax));
            ecb.SetComponent(torusE, new TorusCone(angle));
            ecb.SetComponent(torusE, Collider.TorusCone(radiusMin/radiusMax, 1, angle, math.right()));
            ecb.SetComponent(torusE, new SpawnTimeCreated(Time));
            ecb.SetComponent(torusE, new DestroyAtTime(Time + delay));
            return torusE;
        }

        public static void SpawnTrapProjectile(in DynamicBuffer<Prefabs> prefabs, ref EntityCommandBuffer ecb, in NetworkId parentNetworkId, in LocalTransform localTransform, in double Time)
        {
            var trapProjE = ecb.Instantiate(prefabs[4].Entity);
            ecb.SetComponent(trapProjE, LocalTransform.FromPositionRotationScale(localTransform.Position, localTransform.Rotation, 0.01f));
            ecb.SetComponent(trapProjE, new EnemyTrapProjectileAnimation(){ ParentId = parentNetworkId });
            ecb.SetComponentEnabled<MovementDisabled>(trapProjE, true);
            ecb.SetComponent(trapProjE, new DestroyAtTime(Time + 2));
        }
    }
    
    public struct Enemies : IBufferElementData
    {
        public Entity Entity;
    }
    
    public struct GemVisual : IBufferElementData
    {
        public int InstancedResourceIndex;
    }
    public struct RingVisual : IBufferElementData
    {
        public int InstancedResourceIndex;
    }
    public struct RingDropTemplate : IBufferElementData
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
    
    public SpecificPrefabDatabase SpecificPrefabDatabase;
    public InstancedResourcesDatabase InstancedResourcesDatabase;
    public ParticleDatabase ParticleDatabase;
    public TerrainAuthoring[] Terrains;
    public ProjectileAuthoring[] Projectiles;
    public EnemyCharacterAuthoring[] Enemies;
    public GemVisuals GemVisuals;
    public RingVisuals RingVisuals;
    public RingDropAuthoring[] RingDropTemplates;
    public SurvivorAuthoring[] Survivors;
    public GenericDatabase Prefabs;

    public class Baker : Baker<GameManagerResourcesAuthoring>
    {
        public override void Bake(GameManagerResourcesAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new GameManager());
            AddComponent(entity, new GameManager.Resources()
            {
                GemDropTemplate = GetEntity(authoring.GemDropTemplate, TransformUsageFlags.WorldSpace),
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
                    buffer.Add(new GameManager.Enemies(){ Entity = GetEntity(authoring.Enemies[i].gameObject, TransformUsageFlags.WorldSpace) });
                }
            }
            
            if (authoring.Prefabs?.Length > 0)
            {
                var buffer = AddBuffer<GameManager.Prefabs>(entity);
                for (int i = 0; i < authoring.Prefabs.Length; i++)
                {
                    buffer.Add(new GameManager.Prefabs(){ Entity = GetEntity(authoring.Prefabs[i].gameObject, TransformUsageFlags.WorldSpace) });
                }
            }
            
            if (authoring.GemVisuals && authoring.GemVisuals.Value?.Count > 0)
            {
                if (!authoring.InstancedResourcesDatabase)
                {
                    Debug.LogError($"Couldn't author GemVisuals, instanced resource database null.");
                }
                else
                {
                    var buffer = AddBuffer<GameManager.GemVisual>(entity);
                    buffer.Resize(Enum.GetValues(typeof(Gem.Type)).Length , NativeArrayOptions.ClearMemory);
                    foreach (var kvp in authoring.GemVisuals.Value)
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
            
            if (authoring.RingVisuals && authoring.RingVisuals.Value?.Count > 0)
            {
                if (!authoring.InstancedResourcesDatabase)
                {
                    Debug.LogError($"Couldn't author RingVisuals, instanced resource database null.");
                }
                else
                {
                    var buffer = AddBuffer<GameManager.RingVisual>(entity);
                    buffer.Resize(Enum.GetValues(typeof(RingPrimaryEffect)).Length , NativeArrayOptions.ClearMemory);
                    foreach (var kvp in authoring.RingVisuals.Value)
                    {
                        var index = authoring.InstancedResourcesDatabase.Assets.IndexOf(kvp.Value);
                        if (index == -1)
                        {
                            Debug.LogError($"{kvp.Value} cannot be found in the InstancedResourcesDatabase");
                        }
                        buffer[kvp.Key.GetMostSigBit()] = new GameManager.RingVisual(){ InstancedResourceIndex = index } ;
                    }
                }
            }
            
            if (authoring.RingDropTemplates?.Length > 0)
            {
                var buffer = AddBuffer<GameManager.RingDropTemplate>(entity);
                foreach (var ringDropTemplate in authoring.RingDropTemplates)
                {
                    buffer.Add(new GameManager.RingDropTemplate(){ Entity = GetEntity(ringDropTemplate.gameObject, TransformUsageFlags.WorldSpace) });
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