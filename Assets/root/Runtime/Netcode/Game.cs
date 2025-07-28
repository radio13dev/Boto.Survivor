using System;
using System.Collections.Generic;
using System.Linq;
using BovineLabs.Core.Extensions;
using BovineLabs.Saving;
using BovineLabs.Saving.Data;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Jobs;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.Scripting;

public enum SaveState
{
    Idle,
    Saving,
    Ready
}

public enum GameType
{
    Client,
    Server,
}

[Preserve]
public class Game : IDisposable
{
    public static event Action<Game> OnClientGameStarted;

    public const float k_ClientPingFrequency = 1.0f / 60.0f;
    public const float k_ServerPingFrequency = 1.0f / 60.0f;

    public static bool ConstructorReady => SceneManager.Ready;

    public static Game ServerGame
    {
        get => s_ServerGame;
        set => s_ServerGame = value;
    }
    static Game s_ServerGame;

    public static Game ClientGame
    {
        get => s_ClientGame;
        set
        {
            s_ClientGame = value;
            OnClientGameStarted?.Invoke(s_ClientGame);
        }
    }
    static Game s_ClientGame;

    public World World => m_World;
    public int PlayerIndex = -1;

    private bool m_ShowVisuals;
    private World m_World;
    private EntityQuery m_SaveRequest;
    private EntityQuery m_SaveBuffer;
    private EntityQuery m_RenderSystemHalfTime;
    private SystemHandle m_RenderSystemGroup;

    public NativeQueue<SpecialLockstepActions> RpcSendBuffer;

    private EntityQuery m_PlayerQuery;

    private Entity m_GameManagerSceneE;
    private Entity m_GameSceneE;
    private bool m_Ready;

    public bool IsReady
    {
        get
        {
            if (m_Ready) return true;

            if (m_GameManagerSceneE == Entity.Null || !SceneSystem.IsSceneLoaded(m_World.Unmanaged, m_GameManagerSceneE)) return false;
            if (m_GameSceneE == Entity.Null || !SceneSystem.IsSceneLoaded(m_World.Unmanaged, m_GameSceneE)) return false;

            OnLoadComplete();
            return m_Ready = true;
        }
    }

    public SaveState SaveState
    {
        get
        {
            if (!m_SaveStarted) return SaveState.Idle;

            if (m_SaveRequest.CalculateEntityCount() > 0)
                return SaveState.Saving;

            return SaveState.Ready;
        }
    }

    private bool m_SaveStarted = false;

    public void InitSave()
    {
        m_SaveStarted = true;
        m_World.EntityManager.CreateEntity(ComponentType.ReadOnly<SaveRequest>());
    }

    public void CleanSave()
    {
        m_World.EntityManager.DestroyEntity(m_SaveRequest);
        m_World.EntityManager.DestroyEntity(m_SaveBuffer);
        m_SaveStarted = false;
    }

    public Game(bool showVisuals)
    {
        Debug.Log($"Creating Game...");
        m_World = new World("Game", WorldFlags.Game);
        var systems = DefaultWorldInitialization
            .GetAllSystems(showVisuals ? WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.Presentation : WorldSystemFilterFlags.ServerSimulation).ToList();
        systems.RemoveAll(s => s.Name == typeof(UpdateWorldTimeSystem).Name);
        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(m_World, systems);

        var saveRequest = new EntityQueryDesc
        {
            Any = new ComponentType[]
            {
                typeof(SaveRequest),
            },
            Options = EntityQueryOptions.Default
        };
        m_SaveRequest = m_World.EntityManager.CreateEntityQuery(saveRequest);
        var saveBuffer = new EntityQueryDesc
        {
            Any = new ComponentType[]
            {
                typeof(SaveBuffer),
            },
            Options = EntityQueryOptions.Default
        };
        m_SaveBuffer = m_World.EntityManager.CreateEntityQuery(saveBuffer);

        m_ShowVisuals = showVisuals;
        if (m_ShowVisuals)
        {
            m_RenderSystemHalfTime = m_World.EntityManager.CreateEntityQuery(new ComponentType(typeof(RenderSystemHalfTime)));
            m_RenderSystemGroup = m_World.GetExistingSystem<RenderSystemGroup>();
        }

        RpcSendBuffer = new NativeQueue<SpecialLockstepActions>(Allocator.Persistent);
        
        var playerQuery = new EntityQueryDesc
        {
            Any = new ComponentType[]
            {
                typeof(PlayerControlled),
            },
            Options = EntityQueryOptions.Default
        };
        m_PlayerQuery = m_World.EntityManager.CreateEntityQuery(playerQuery);
    }

    public void LoadScenes()
    {
        Debug.Log($"Loading subscene with GUID: {SceneManager.GameManagerScene.SceneGUID}");
        m_GameManagerSceneE = SceneSystem.LoadSceneAsync(m_World.Unmanaged, SceneManager.GameManagerScene.SceneGUID);

        Debug.Log($"Loading subscene with GUID: {SceneManager.GameScene.SceneGUID}");
        m_GameSceneE = SceneSystem.LoadSceneAsync(m_World.Unmanaged, SceneManager.GameScene.SceneGUID);
    }

    public void Dispose()
    {
        RpcSendBuffer.Dispose();
        m_World.Dispose();
    }

    public unsafe void SendSave(ref DataStreamWriter writer)
    {
        EntityManager entityManager = m_World.EntityManager;
        var saveBufferEntities = m_SaveBuffer.ToEntityArray(Allocator.Temp);
        if (saveBufferEntities.Length > 1)
        {
            Debug.Log($"Multiple save buffers found, weird: {saveBufferEntities.Length}");
            for (int i = 0; i < saveBufferEntities.Length; i++)
            {
                var weirdBuffer = entityManager.GetBuffer<SaveBuffer>(saveBufferEntities[i]);
                Debug.Log($"{i}: {weirdBuffer.Length}");
            }
        }
        else if (saveBufferEntities.Length == 0)
        {
            Debug.LogError($"No save buffer found, failed send.");
            return;
        }

        var saveBuffer = entityManager.GetBuffer<SaveBuffer>(saveBufferEntities[0]);
        var reint = saveBuffer.Reinterpret<byte>();
        writer.WriteInt(reint.Length);
        writer.WriteBytes(reint.AsNativeArray());
        Debug.Log($"... wrote {reint.Length}...");
    }

    public unsafe void LoadSave(NativeArray<byte> ptr)
    {
        EntityManager entityManager = m_World.EntityManager;
        var loadBufferE = entityManager.CreateEntity(ComponentType.ReadWrite<LoadBuffer>());
        var loadBuffer = entityManager.GetBuffer<LoadBuffer>(loadBufferE);
        loadBuffer.AddRange(ptr.Reinterpret<LoadBuffer>());
        Debug.Log($"... save loaded...");
        m_Ready = false; // Unset ready.
    }

    public unsafe void ApplyStepData(FullStepData stepData, SpecialLockstepActions* extraActionPtr)
    {
        if (!m_World.GetExistingSystemManaged<SurvivorSimulationSystemGroup>().Enabled)
        {
            Debug.LogError($"Failed step, tried to go to step {stepData.Step} but simulation disabled.");
            return;
        }
        
        var entityManager = m_World.EntityManager;

        // Iterate step count
        {
            var query = entityManager.CreateEntityQuery(new ComponentType(typeof(StepController)));
            var stepController = query.GetSingleton<StepController>();

            if (stepData.Step != stepController.Step + 1)
            {
                Debug.LogError($"Failed step, tried to go from step {stepController.Step} to {stepData.Step}");
                return;
            }

            entityManager.SetComponentData(query.GetSingletonEntity(), new StepController()
            {
                Step = stepData.Step
            });
        }

        // Setup time for the frame
        m_World.SetTime(new TimeData(stepData.Step * (double)Game.k_ClientPingFrequency, Game.k_ClientPingFrequency));
        if (m_ShowVisuals) m_RenderSystemHalfTime.SetSingleton(new RenderSystemHalfTime() { Value = 0 });

        // Apply extra actions
        {
            if (stepData.ExtraActionCount > 0)
            {
                Debug.Log($"Step {stepData.Step}: Applying {stepData.ExtraActionCount} extra actions");
                for (byte i = 0; i < stepData.ExtraActionCount; i++)
                    extraActionPtr[i].Apply(this);
            }
        }

        // Apply inputs
        {
            var query = entityManager.CreateEntityQuery(new ComponentType(typeof(StepInput)), new ComponentType(typeof(PlayerControlled)));
            var entities = query.ToEntityArray(Allocator.Temp);
            if (entities.Length > 0)
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var index = entityManager.GetComponentData<PlayerControlled>(entities[i]).Index;
                    var data = stepData[index];
                    entityManager.SetComponentData(entities[i], data);
                }
            }

            entities.Dispose();
            query.Dispose();
        }

        if (ClientDesyncDebugger.Instance)
        {
            ClientDesyncDebugger.Instance.UpdateGame(stepData.Step, this);
            ClientDesyncDebugger.Instance.CleanGameSaves();
        }
        else
            m_World.Update();
        
        CollectDataForUI();
    }

    public void ApplyRender(float t)
    {
        m_RenderSystemHalfTime.SetSingleton(new RenderSystemHalfTime() { Value = t });
        m_RenderSystemGroup.Update(m_World.Unmanaged);
    }
    
    public Action<PlayerDataCache> OnInventoryUpdated;
    public List<PlayerDataCache> m_PlayerDataCaches = new();
    public void CollectDataForUI()
    {
        for (int i = 0; i < m_PlayerDataCaches.Count; i++)
        {
            if (m_PlayerDataCaches[i].Dirty)
            {
                m_PlayerDataCaches[i].Clean(this);
            }
        }
    }

    private void OnLoadComplete()
    {
        // Fetch any 'initial' cache data
        var query = m_World.EntityManager.CreateEntityQuery(new ComponentType(typeof(PlayerControlled)));
        var players = query.ToComponentDataArray<PlayerControlled>(Allocator.Temp);
        for (int i = 0; i < players.Length; i++)
            m_PlayerDataCaches.Add(new PlayerDataCache(players[i].Index));
        players.Dispose();
        query.Dispose();
        
        // Also enable the simulation system group
        m_World.GetExistingSystemManaged<SurvivorSimulationSystemGroup>().Enabled = true;
    }


    public static void SetCacheDirty(Entity entity)
    {
        Debug.Log($"Cleaning cache...");
        foreach (var cache in ClientGame.m_PlayerDataCaches)
            if (cache.PlayerE == entity) 
                cache.SetDirty();
    }

    public void Update_NoLogic()
    {
        var oldEnabled = m_World.GetExistingSystemManaged<SurvivorSimulationSystemGroup>().Enabled;
        m_World.GetExistingSystemManaged<SurvivorSimulationSystemGroup>().Enabled = false;
        m_World.Update();
        m_World.GetExistingSystemManaged<SurvivorSimulationSystemGroup>().Enabled = oldEnabled;
    }

    public NativeArray<byte> InstantSave()
    {
        InitSave();
        do
        {
            Update_NoLogic();
            CompleteDependencies();
        }
        while (SaveState == SaveState.Saving);
        
        EntityManager entityManager = m_World.EntityManager;
        var saveBufferEntities = m_SaveBuffer.ToEntityArray(Allocator.Temp);
        if (saveBufferEntities.Length > 1)
        {
            Debug.Log($"Multiple save buffers found, weird: {saveBufferEntities.Length}");
            for (int i = 0; i < saveBufferEntities.Length; i++)
            {
                var weirdBuffer = entityManager.GetBuffer<SaveBuffer>(saveBufferEntities[i]);
                Debug.Log($"{i}: {weirdBuffer.Length}");
            }
        }
        else if (saveBufferEntities.Length == 0)
        {
            throw new Exception("No save buffer found, failed send.");
        }

        var saveBuffer = entityManager.GetBuffer<SaveBuffer>(saveBufferEntities[0]);
        var reint = saveBuffer.Reinterpret<byte>();
        var data = reint.ToNativeArray(Allocator.Persistent);
        //Debug.Log($"... wrote {reint.Length}...");
        CleanSave();
        return data;
    }

    public void CompleteDependencies()
    {
        NativeList<JobHandle> dependencies = new NativeList<JobHandle>(Allocator.Temp);
        m_World.Unmanaged.GetAllSystemDependencies(dependencies);
        for (int i = 0; i < dependencies.Length; i++)
        {
            dependencies[i].Complete();
        }
        dependencies.Clear();
    }
}

[Save]
public struct PlayerControlled : IComponentData
{
    public int Index;
}

public class PlayerDataCache
{
    public int PlayerIndex => m_PlayerIndex;
    private int m_PlayerIndex;

    public bool Dirty => m_Dirty;
    private bool m_Dirty = true;
    
    private EntityQuery m_PlayerQuery;
    
    public Entity PlayerE => m_PlayerE;
    private Entity m_PlayerE;
    
    public Ring[] Rings => m_Rings;
    private Ring[] m_Rings;

    public PlayerDataCache(int playerIndex)
    {
        m_PlayerIndex = playerIndex;
        m_Rings = new Ring[Ring.k_RingCount];
    }

    public void SetDirty()
    {
        m_Dirty = true;
    }

    public void Clean(Game game)
    {
        var world = game.World;
        var entityManager = world.EntityManager;
        
        ValidatePlayerE(entityManager);
        if (m_PlayerE == Entity.Null)
            return;
                
        m_Dirty = false;

        
        ValidateRings(entityManager);
        game.OnInventoryUpdated?.Invoke(this);
    }

    private void ValidatePlayerE(EntityManager entityManager)
    {
        if (!entityManager.HasComponent<PlayerControlled>(m_PlayerE) || entityManager.GetComponentData<PlayerControlled>(m_PlayerE).Index != PlayerIndex)
        {
            if (m_PlayerQuery == default)
                m_PlayerQuery = entityManager.CreateEntityQuery(new ComponentType(typeof(PlayerControlled)));
            
            var players = m_PlayerQuery.ToComponentDataArray<PlayerControlled>(Allocator.Temp);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].Index == PlayerIndex)
                {
                    var playersE = m_PlayerQuery.ToEntityArray(Allocator.Temp);
                    m_PlayerE = playersE[i];
                    playersE.Dispose();
                    break;
                }
            }
            players.Dispose();
        }
    }

    private void ValidateRings(EntityManager entityManager)
    {
        if (!entityManager.HasBuffer<Ring>(m_PlayerE))
        {
            m_Rings = new Ring[Ring.k_RingCount];
            return;
        }
        
        var buffer = entityManager.GetBuffer<Ring>(m_PlayerE);
        for (int i = 0; i < buffer.Length; i++)
            m_Rings[i] = buffer[i];
    }
}