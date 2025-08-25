using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BovineLabs.Core.Extensions;
using BovineLabs.Saving;
using BovineLabs.Saving.Data;
using Unity.Burst;
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

public interface IGameFactory
{
    Game Invoke();
}

public class GameFactory : IGameFactory
{
    public bool ShowVisuals;
    public IStepProvider StepProvider;
    public string WorldName;

    public GameFactory(string worldName, bool showVisuals = true, IStepProvider stepProvider = null)
    {
        WorldName = worldName;
        ShowVisuals = showVisuals;
        StepProvider = stepProvider ?? new RateStepProvider();
    }

    public Game Invoke()
    {
        return new Game(WorldName, ShowVisuals, StepProvider);
    }
}

[Preserve]
public class Game : IDisposable
{
    public const double DefaultDt = 1.0f / 60.0d;
    public static bool ConstructorReady => SubsceneSceneManager.Ready;

    public static Game ClientGame
    {
        get => s_ClientGame;
        set
        {
            if (s_ClientGame == value) return;
            s_ClientGame = value;
            CameraTarget.MainTarget = null;
        }
    }

    static Game s_ClientGame;

    public World World => m_World;
    public int PlayerIndex = -1;
    public long Step => m_StepController.GetSingleton<StepController>().Step;

    private IStepProvider m_StepProvider;
    private bool m_ShowVisuals;
    private World m_World;
    private EntityQuery m_SaveRequest;
    private EntityQuery m_SaveBuffer;
    private EntityQuery m_RenderSystemHalfTime;
    private EntityQuery m_StepController;
    private SystemHandle m_RenderSystemGroup;

    public NativeQueue<GameRpc> RpcSendBuffer;

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
        Update_NoLogic(); // This should 'resolve' any ECBs that are pending, so we can save the current state.
        m_World.EntityManager.CreateEntity(ComponentType.ReadOnly<SaveRequest>());
    }

    public void CleanSave()
    {
        m_World.EntityManager.DestroyEntity(m_SaveRequest);
        m_World.EntityManager.DestroyEntity(m_SaveBuffer);
        m_SaveStarted = false;
    }

    public Game(string worldName, bool showVisuals, IStepProvider stepProvider)
    {
        m_StepProvider = stepProvider;
        m_ShowVisuals = showVisuals;

        Debug.Log($"Creating Game...");
        m_World = new World(worldName, WorldFlags.Game);
        var systems = DefaultWorldInitialization
            .GetAllSystems(showVisuals ? WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.Presentation : WorldSystemFilterFlags.ServerSimulation).ToList();
        systems.RemoveAll(s => s.Name == typeof(UpdateWorldTimeSystem).Name);
        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(m_World, systems);
        
        if (m_ShowVisuals)
        {
            m_World.GetExistingSystemManaged<GenericPrefabSpawnSystem>().GameReference = this;
            m_World.GetExistingSystemManaged<SpecificPrefabSpawnSystem>().GameReference = this;
        }

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

        if (m_ShowVisuals)
        {
            m_RenderSystemHalfTime = m_World.EntityManager.CreateEntityQuery(new ComponentType(typeof(RenderSystemHalfTime)));
            m_RenderSystemGroup = m_World.GetExistingSystem<RenderSystemGroup>();
        }

        RpcSendBuffer = new NativeQueue<GameRpc>(Allocator.Persistent);

        m_StepController = m_World.EntityManager.CreateEntityQuery(new ComponentType(typeof(StepController)));
    }

    public void LoadScenes()
    {
        Debug.Log($"Loading subscene with GUID: {SubsceneSceneManager.GameManagerScene.SceneGUID}");
        m_GameManagerSceneE = SceneSystem.LoadSceneAsync(m_World.Unmanaged, SubsceneSceneManager.GameManagerScene.SceneGUID);

        Debug.Log($"Loading subscene with GUID: {SubsceneSceneManager.GameScene.SceneGUID}");
        m_GameSceneE = SceneSystem.LoadSceneAsync(m_World.Unmanaged, SubsceneSceneManager.GameScene.SceneGUID);
    }

    public void Dispose()
    {
        RpcSendBuffer.Dispose();
        if (m_World.IsCreated)
        {
            //m_World.DestroyAllSystemsAndLogException(out _);
            m_World.Dispose();
        }
    }

    public unsafe void SendSave(ref DataStreamWriter writer)
    {
        CompleteDependencies();
        
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

    public bool CanStep()
    {
        return m_StepProvider.CanStep(this);
    }

    public unsafe void ApplyStepData(FullStepData stepData, GameRpc* extraActionPtr)
    {
        m_StepProvider.ExecuteStep();

        if (!m_World.GetExistingSystemManaged<SurvivorSimulationSystemGroup>().Enabled)
        {
            Debug.LogError($"Failed step, tried to go to step {stepData.Step} but simulation disabled.");
            return;
        }

        var entityManager = m_World.EntityManager;

        // Iterate step count
        {
            var stepController = m_StepController.GetSingleton<StepController>();

            if (stepData.Step != stepController.Step + 1)
            {
                Debug.LogWarning($"Failed step, tried to go from step {stepController.Step} to {stepData.Step}");
                return;
            }

            m_StepController.SetSingleton(new StepController()
            {
                Step = stepData.Step
            });
        }

        // Setup time for the frame
        m_World.SetTime(new TimeData(stepData.Step * Game.DefaultDt, (float)Game.DefaultDt));
        if (m_ShowVisuals)
        {
            m_RenderSystemHalfTime.SetSingleton(new RenderSystemHalfTime() { Value = m_StepProvider.HalfTime });
        }

        // Apply extra actions
        {
            if (stepData.ExtraActionCount > 0)
            {
                Debug.Log($"{World.Name} Step {stepData.Step}: Applying {stepData.ExtraActionCount} extra actions");
                for (byte i = 0; i < stepData.ExtraActionCount; i++)
                    extraActionPtr[i].Apply(this);
            }
        }

        // Apply inputs
        {
            using var query = entityManager.CreateEntityQuery(typeof(StepInput), typeof(PlayerControlled));
            for (int i = 0; i < stepData.Length; i++)
            {
                query.SetSharedComponentFilter(new PlayerControlled() { Index = i });
                if (query.CalculateEntityCount() == 0)
                {
                    continue;
                }

                using var entities = query.ToEntityArray(Allocator.Temp);
                for (int j = 0; j < entities.Length; j++)
                    entityManager.SetComponentData(entities[j], stepData[i]);
            }
        }

        m_World.Update();
    }

    public void ApplyRender()
    {
        if (!m_ShowVisuals) return;
        m_RenderSystemHalfTime.SetSingleton(new RenderSystemHalfTime() { Value = m_StepProvider.HalfTime });
        m_RenderSystemGroup.Update(m_World.Unmanaged);
    }

    private void OnLoadComplete()
    {
        // Do one more update
        Update_NoLogic();
        // Perform the 'init after load'
        NetworkIdSystem.InitAfterLoad(m_World.EntityManager);
        // Also enable the simulation system group
        m_World.GetExistingSystemManaged<SurvivorSimulationSystemGroup>().Enabled = true;
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
        } while (SaveState == SaveState.Saving);

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

    public void CleanForSingleplayer()
    {
        for (int i = 0; i < PingServerBehaviour.k_MaxPlayerCount; i++)
        {
            if (i == PlayerIndex) continue;
            new GameRpc() { Type = GameRpc.Code.PlayerLeave, PlayerId = (byte)i }.Apply(this);
        }
    }

    public void InitWorld()
    {
        World.EntityManager.SetSingleton(new SharedRandom(){ Random = Unity.Mathematics.Random.CreateFromIndex((uint)(DateTime.UtcNow.Ticks % uint.MaxValue))});
        var initSystemGroup = World.GetExistingSystemManaged<WorldInitSystemGroup>();
        initSystemGroup.Enabled = true;
        initSystemGroup.Update();
        initSystemGroup.Enabled = false;
    }
}

public static class GameEvents
{
    public enum Type
    {
        InventoryChanged,
        PlayerHealthChanged,
        InteractableStart,
        InteractableEnd,
        VisualsUpdated
    }

    public static event Action<Type, Entity> OnEvent;

    static readonly SharedStatic<NativeQueue<(Type, Entity)>> s_EventQueue = SharedStatic<NativeQueue<(Type, Entity)>>.GetOrCreate<NativeQueue<(Type, Entity)>, EventQueueKey>();

    private class EventQueueKey
    {
    }

    static bool m_Initialized;

    public static void Initialize()
    {
        if (m_Initialized)
        {
            throw new Exception($"{nameof(GameEvents)}.{nameof(Initialize)}() ran multiple times.");
            return;
        }

        m_Initialized = true;
        s_EventQueue.Data = new NativeQueue<(Type, Entity)>(Allocator.Persistent);
    }

    public static void Dispose()
    {
        s_EventQueue.Data.Dispose();
        m_Initialized = false;
    }

    public static void ExecuteEvents()
    {
        while (s_EventQueue.Data.TryDequeue(out var eType))
        {
            OnEvent?.Invoke(eType.Item1, eType.Item2);
        }
    }

    public static void Trigger(Type eType, Entity entity)
    {
        s_EventQueue.Data.Enqueue((eType, entity));
    }

    public static bool TryGetSharedComponent<T>(Entity entity, out T o) where T : unmanaged, ISharedComponentData
    {
        if (Game.ClientGame == null)
        {
            o = default;
            return false;
        }

        if (Game.ClientGame.World.EntityManager.HasComponent<T>(entity))
        {
            o = Game.ClientGame.World.EntityManager.GetSharedComponent<T>(entity);
            return true;
        }

        o = default;
        return false;
    }
    
    public static bool TryGetComponent2<T>(Entity entity, out T o) where T : unmanaged, IComponentData
    {
        if (Game.ClientGame == null)
        {
            o = default;
            return false;
        }

        if (Game.ClientGame.World.EntityManager.HasComponent<T>(entity))
        {
            o = Game.ClientGame.World.EntityManager.GetComponentData<T>(entity);
            return true;
        }

        o = default;
        return false;
    }

    public static bool TryGetBuffer<T>(Entity entity, out DynamicBuffer<T> o) where T : unmanaged, IBufferElementData
    {
        if (Game.ClientGame == null)
        {
            o = default;
            return false;
        }

        if (Game.ClientGame.World.EntityManager.HasBuffer<T>(entity))
        {
            o = Game.ClientGame.World.EntityManager.GetBuffer<T>(entity, true);
            return true;
        }

        o = default;
        return false;
    }
}