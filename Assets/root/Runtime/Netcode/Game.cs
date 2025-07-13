using System;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve] 
public class DisableBootstrap : ICustomBootstrap
{
    public bool Initialize(string defaultWorldName)
    {
        var emptyWorld = new World("Empty");
        World.DefaultGameObjectInjectionWorld = emptyWorld;
        return true;
    }
}

[Preserve]
public class Game : IDisposable
{
    public World World => m_World;

    private World m_World;
    private EntityQuery m_EntitiesToSave;
    
    private Entity m_GameManagerSceneE;
    private Entity m_GameSceneE;
    private bool m_Ready;

    public Game()
    {
        Debug.Log($"Creating Game...");
        m_World = new World("Game", WorldFlags.Game);
        var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default).ToList();
        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(m_World, systems);
        
        Debug.Log($"Loading subscene with GUID: {SceneManager.GameManagerScene.SceneGUID}");
        m_GameManagerSceneE = SceneSystem.LoadSceneAsync(m_World.Unmanaged, SceneManager.GameManagerScene.SceneGUID);
        Debug.Log($"Loading subscene with GUID: {SceneManager.GameScene.SceneGUID}");
        m_GameSceneE = SceneSystem.LoadSceneAsync(m_World.Unmanaged, SceneManager.GameScene.SceneGUID);
        
        
        var savableEntities = new EntityQueryDesc
        {
            Any = new ComponentType[]
            {
                typeof(SaveTag),
            },
            Options = EntityQueryOptions.Default
        };
        m_EntitiesToSave = m_World.EntityManager.CreateEntityQuery(savableEntities);
    }

    public bool IsReady
    {
        get
        {
            if (m_Ready) return true;
            return m_Ready = SceneSystem.IsSceneLoaded(m_World.Unmanaged, m_GameManagerSceneE) && SceneSystem.IsSceneLoaded(m_World.Unmanaged, m_GameSceneE);
        }
    }

    

    public void Dispose()
    {
        m_EntitiesToSave.Dispose();
        m_World.Dispose();
    }
    
    

    // Looks for and removes a set of components and then adds a different set of components to the same set
    // of entities. 
    private void ReplaceComponents(
        ComponentType[] typesToRemove,
        ComponentType[] typesToAdd,
        EntityManager entityManager)
    {
        EntityQuery query = entityManager.CreateEntityQuery(
            new EntityQueryDesc { Any = typesToRemove, Options = EntityQueryOptions.Default }
        );
        NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);

        foreach (ComponentType removeType in typesToRemove)
        {
            entityManager.RemoveComponent(entities, removeType);
        }

        foreach (ComponentType addType in typesToAdd)
        {
            entityManager.AddComponent(entities, addType);
        }
    }

    public void SaveManual()
    {
        Save(Path.Combine(Application.persistentDataPath, "Saves", "01"));
    }

    public void LoadManual()
    {
        Load(Path.Combine(Application.persistentDataPath, "Saves", "01"));
    }

    public unsafe void Save(string filepath)
    {
        /*
         * 1. Create a new world.
         * 2. Copy over the entities we want to serialize to the new world.
         * 3. Remove all shared components, components containing blob asset references, and components containing
         *    external entity references.
         * 4. Serialize the new world to a save file.
         */

        EntityManager entityManager = m_World.EntityManager;
        using (var serializeWorld = new World("Serialization World"))
        {
            EntityManager serializeEntityManager = serializeWorld.EntityManager;
            SetupSerializeWorld(ref serializeEntityManager, ref entityManager, ref m_EntitiesToSave);

            // Save
            using (var writer = new MemoryBinaryWriter())
            using (var fileWriter = new StreamWriter(filepath))
            {
                SerializeUtility.SerializeWorld(serializeEntityManager, writer);
                fileWriter.Write(new Span<char>(writer.Data, writer.Length / 2 + 1));
            }
        }
    }

    public unsafe void SendSave(ref DataStreamWriter writer)
    {
        EntityManager entityManager = m_World.EntityManager;
        using (var serializeWorld = new World("Serialization World"))
        {
            EntityManager serializeEntityManager = serializeWorld.EntityManager;
            SetupSerializeWorld(ref serializeEntityManager, ref entityManager, ref m_EntitiesToSave);

            // Save
            using (var memwriter = new MemoryBinaryWriter())
            {
                SerializeUtility.SerializeWorld(serializeEntityManager, memwriter);
                writer.WriteInt(memwriter.Length);
                writer.WriteBytes(new Span<byte>(memwriter.Data, memwriter.Length));
                Debug.Log($"... wrote {memwriter.Length}...");
            }
        }
    }

    public unsafe void Load(string filepath)
    {
        EntityManager entityManager = m_World.EntityManager;
        entityManager.DestroyEntity(m_EntitiesToSave);

        using (var deserializeWorld = new World("Deserialization World"))
        {
            ExclusiveEntityTransaction transaction = deserializeWorld.EntityManager.BeginExclusiveEntityTransaction();

            var fileData = File.ReadAllBytes(filepath);
            fixed (byte* ptr = &fileData[0])
            {
                using (var reader = new MemoryBinaryReader(ptr, fileData.Length))
                {
                    SerializeUtility.DeserializeWorld(transaction, reader);
                }
            }

            deserializeWorld.EntityManager.EndExclusiveEntityTransaction();

            entityManager.MoveEntitiesFrom(deserializeWorld.EntityManager);
        }
    }

    public unsafe void LoadSave(byte* ptr, int len)
    {
        // Read THAT into the world
        EntityManager entityManager = m_World.EntityManager;
        entityManager.DestroyEntity(m_EntitiesToSave);

        using (var deserializeWorld = new World("Deserialization World"))
        {
            ExclusiveEntityTransaction transaction = deserializeWorld.EntityManager.BeginExclusiveEntityTransaction();

            Debug.Log($"... setting up reader ...");
            using (var memreader = new MemoryBinaryReader(ptr, len))
            {
                Debug.Log($"... deserializing ...");
                SerializeUtility.DeserializeWorld(transaction, memreader);
            }
            
            Debug.Log($"... wrapping up ...");

            deserializeWorld.EntityManager.EndExclusiveEntityTransaction();

            entityManager.MoveEntitiesFrom(deserializeWorld.EntityManager);
        }
    }
    
    public unsafe void ReloadSave()
    {
        using (var writer = new MemoryBinaryWriter())
        {
            EntityManager entityManager = m_World.EntityManager;
            using (var serializeWorld = new World("Serialization World"))
            {
                EntityManager serializeEntityManager = serializeWorld.EntityManager;
                SetupSerializeWorld(ref serializeEntityManager, ref entityManager, ref m_EntitiesToSave);

                // Save
                {
                    SerializeUtility.SerializeWorld(serializeEntityManager, writer);
                }
            }
            
            entityManager.DestroyEntity(m_EntitiesToSave);
            using (var deserializeWorld = new World("Deserialization World"))
            {
                ExclusiveEntityTransaction transaction = deserializeWorld.EntityManager.BeginExclusiveEntityTransaction();

                Debug.Log($"... setting up reader ...");
                using (var memreader = new MemoryBinaryReader(writer.Data, writer.Length))
                {
                    Debug.Log($"... deserializing ...");
                    SerializeUtility.DeserializeWorld(transaction, memreader);
                }
            
                Debug.Log($"... wrapping up ...");

                deserializeWorld.EntityManager.EndExclusiveEntityTransaction();

                entityManager.MoveEntitiesFrom(deserializeWorld.EntityManager);
            }
        }

    }

    private static void SetupSerializeWorld(ref EntityManager serializeEntityManager, ref EntityManager entityManager, ref EntityQuery m_EntitiesToSave)
    {
        serializeEntityManager.CopyEntitiesFrom(entityManager, m_EntitiesToSave.ToEntityArray(Allocator.Temp));
        
        // Need to remove the SceneTag shared component from all entities because it contains an entity reference
        // that exists outside the subscene which isn't allowed for SerializeUtility. This breaks the link from the
        // entity to the subscene, but otherwise doesn't seem to cause any problems.
        serializeEntityManager.RemoveComponent<SceneTag>(serializeEntityManager.UniversalQuery);
        serializeEntityManager.RemoveComponent<SceneSection>(serializeEntityManager.UniversalQuery);
            
        // Also remove proxies and requests
        serializeEntityManager.RemoveComponent<GenericPrefabProxy>(serializeEntityManager.UniversalQuery);
        serializeEntityManager.RemoveComponent<SpecificPrefabProxy>(serializeEntityManager.UniversalQuery);
    }
}

public partial struct StepSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        var stepControllerEntity = state.EntityManager.CreateSingleton<StepController>();
        state.EntityManager.AddComponent<SaveTag>(stepControllerEntity);
    }
}

public struct StepController : IComponentData
{
    public long Step;

    public StepController(long step)
    {
        Step = step;
    }
}

public struct PlayerControlled : IComponentData
{
    public int Index;
}