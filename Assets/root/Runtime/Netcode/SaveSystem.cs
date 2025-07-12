using System;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Rendering;
using UnityEngine;
using BinaryReader = Unity.Entities.Serialization.BinaryReader;

public struct SaveTag : IComponentData
{
}

/// <summary>
/// From: https://gist.github.com/ScottJDaley/52f937c266247f221341d3fd4244e737#file-savemanager-cs
/// </summary>
public class SaveManager : IDisposable
{
    private World _world;
    private EntityQuery _entitiesToSave;

    public SaveManager(World world)
    {
        _world = world;
        // Cache a query that gathers all of the entities that should be saved.
        // NOTE: You don't have to use a special tag component for all entities you want to save. You could instead just
        // save, for example, anything with a Translation component which would exclude things like Singletons entities.
        // It is important to note that prefabs (anything with a Prefab tag component) are automatically excluded from
        // an EntityQuery unless EntityQueryOptions.IncludePrefab is set.
        var savableEntities = new EntityQueryDesc
        {
            Any = new ComponentType[]
            {
                typeof(SaveTag),
            },
            Options = EntityQueryOptions.Default
        };
        _entitiesToSave = world.EntityManager.CreateEntityQuery(savableEntities);
    }

    public void Dispose()
    {
        _entitiesToSave.Dispose();
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

        EntityManager entityManager = _world.EntityManager;
        using (var serializeWorld = new World("Serialization World"))
        {
            EntityManager serializeEntityManager = serializeWorld.EntityManager;
            serializeEntityManager.CopyEntitiesFrom(entityManager, _entitiesToSave.ToEntityArray(Allocator.Temp));

            // Need to remove the SceneTag shared component from all entities because it contains an entity reference
            // that exists outside the subscene which isn't allowed for SerializeUtility. This breaks the link from the
            // entity to the subscene, but otherwise doesn't seem to cause any problems.
            serializeEntityManager.RemoveComponent<SceneTag>(serializeEntityManager.UniversalQuery);
            serializeEntityManager.RemoveComponent<SceneSection>(serializeEntityManager.UniversalQuery);
            
            // Also remove proxies and requests
            serializeEntityManager.RemoveComponent<GenericPrefabRequest>(serializeEntityManager.UniversalQuery);

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
        EntityManager entityManager = _world.EntityManager;
        using (var serializeWorld = new World("Serialization World"))
        {
            EntityManager serializeEntityManager = serializeWorld.EntityManager;
            serializeEntityManager.CopyEntitiesFrom(_world.EntityManager, _entitiesToSave.ToEntityArray(Allocator.Temp));

            // Need to remove the SceneTag shared component from all entities because it contains an entity reference
            // that exists outside the subscene which isn't allowed for SerializeUtility. This breaks the link from the
            // entity to the subscene, but otherwise doesn't seem to cause any problems.
            serializeEntityManager.RemoveComponent<SceneTag>(serializeEntityManager.UniversalQuery);
            serializeEntityManager.RemoveComponent<SceneSection>(serializeEntityManager.UniversalQuery);
            
            // Also remove proxies and requests
            serializeEntityManager.RemoveComponent<GenericPrefabProxy>(serializeEntityManager.UniversalQuery);

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
        EntityManager entityManager = _world.EntityManager;
        entityManager.DestroyEntity(_entitiesToSave);

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
        EntityManager entityManager = _world.EntityManager;
        entityManager.DestroyEntity(_entitiesToSave);

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
            EntityManager entityManager = _world.EntityManager;
            using (var serializeWorld = new World("Serialization World"))
            {
                EntityManager serializeEntityManager = serializeWorld.EntityManager;
                serializeEntityManager.CopyEntitiesFrom(entityManager, _entitiesToSave.ToEntityArray(Allocator.Temp));

                // Need to remove the SceneTag shared component from all entities because it contains an entity reference
                // that exists outside the subscene which isn't allowed for SerializeUtility. This breaks the link from the
                // entity to the subscene, but otherwise doesn't seem to cause any problems.
                serializeEntityManager.RemoveComponent<SceneTag>(serializeEntityManager.UniversalQuery);
                serializeEntityManager.RemoveComponent<SceneSection>(serializeEntityManager.UniversalQuery);
            
                // Also remove proxies and requests
                serializeEntityManager.RemoveComponent<GenericPrefabProxy>(serializeEntityManager.UniversalQuery);

                // Save
                {
                    SerializeUtility.SerializeWorld(serializeEntityManager, writer);
                }
            }
            
            entityManager.DestroyEntity(_entitiesToSave);
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
}