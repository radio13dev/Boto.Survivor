using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using BovineLabs.Core.Extensions;
using BovineLabs.Saving;
using BovineLabs.Saving.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;

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

public class LobbyFactory : IGameFactory
{
    public bool ShowVisuals;
    public IStepProvider StepProvider;
    public string WorldName;

    public LobbyFactory(string worldName, bool showVisuals = true, IStepProvider stepProvider = null)
    {
        WorldName = worldName;
        ShowVisuals = showVisuals;
        StepProvider = stepProvider ?? new RateStepProvider();
    }

    public Game Invoke()
    {
        return new Game(WorldName, ShowVisuals, StepProvider, 0);
    }
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
        return new Game(WorldName, ShowVisuals, StepProvider, 1);
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
            OnClientGameChanged?.Invoke();
            CameraTarget.MainTarget = null;
        }
    }

    static Game s_ClientGame;
    public static event Action OnClientGameChanged;
    
    public static LinkedList<Game> AllGames = new();
    
    public byte PlayerIndex
    {
        get
        {
            return m_PlayerIndex;
        }
        set
        {
            m_PlayerIndex = value;
            ClientPlayerIndex.Data = value;
        }
    }
    public byte m_PlayerIndex = byte.MaxValue;
    
    public static readonly SharedStatic<int> ClientPlayerIndex = SharedStatic<int>.GetOrCreate<k_ClientPlayerIndex>();
    private class k_ClientPlayerIndex { }

    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        ClientPlayerIndex.Data = -1;
    }

    public World World => m_World;
    public long Step => m_StepController.GetSingleton<StepController>().Step;
    public int GameType => m_GameType;

    private IStepProvider m_StepProvider;
    private bool m_ShowVisuals;
    private int m_GameType;
    private World m_World;
    private EntityQuery m_SaveRequest;
    private EntityQuery m_SaveBuffer;
    private EntityQuery m_RenderSystemHalfTime;
    private EntityQuery m_StepController;
    private SystemHandle m_RenderSystemGroup;

    public NativeQueue<GameRpc> RpcSendBuffer;
    public Dictionary<GameRpc, Action> RpcCallbacks = new();

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

    public Game(string worldName, bool showVisuals, IStepProvider stepProvider, int gameType)
    {
        m_GameType = gameType;
        m_StepProvider = stepProvider;
        m_ShowVisuals = showVisuals;

        Debug.Log($"Creating Game...");
        m_World = new World(worldName, WorldFlags.Game);
        var systems = DefaultWorldInitialization
            .GetAllSystems(showVisuals ? WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.Presentation : WorldSystemFilterFlags.ServerSimulation).ToList();
        systems.RemoveAll(s => s.Name == typeof(UpdateWorldTimeSystem).Name);
        systems.RemoveAll(s => s.GetCustomAttributes(typeof(GameTypeOnlySystemAttribute), true).Any(c => c is GameTypeOnlySystemAttribute gameTypeRestrict && gameTypeRestrict.GameType != m_GameType));
        
        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(m_World, systems);
        m_World.EntityManager.CreateSingleton(new GameTypeSingleton(){Value = gameType});
        
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
        
        AllGames.AddLast(this);
    }

    public void LoadScenes()
    {
        // All games use same manager scene
        Debug.Log($"Loading subscene with GUID: {SubsceneSceneManager.GameManagerScene.SceneGUID}");
        m_GameManagerSceneE = SceneSystem.LoadSceneAsync(m_World.Unmanaged, SubsceneSceneManager.GameManagerScene.SceneGUID);

        // Load game scene for given game type
        var scene = SubsceneSceneManager.GameScenes[GameType];
        Debug.Log($"Loading subscene with GUID: {scene.SceneGUID}");
        m_GameSceneE = SceneSystem.LoadSceneAsync(m_World.Unmanaged, scene.SceneGUID);
    }

    public void Dispose()
    {
        
        if (ClientGame == this)
            ClientGame = null;
        if (RpcSendBuffer.IsCreated) RpcSendBuffer.Dispose();
        if (m_World.IsCreated)
        {
            // Don't need to dispose of the scenes, I think? World dispose should do it.
            //if (m_GameManagerSceneE != Entity.Null) SceneSystem.UnloadScene(m_World.Unmanaged, m_GameManagerSceneE, SceneSystem.UnloadParameters.DestroyMetaEntities);
            //if (m_GameSceneE != Entity.Null) SceneSystem.UnloadScene(m_World.Unmanaged, m_GameSceneE, SceneSystem.UnloadParameters.DestroyMetaEntities);
            
            m_World.Dispose();
            AllGames.Remove(this);
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
            for (byte i = 0; i < PingServerBehaviour.k_MaxPlayerCount; i++)
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
        
        // Do RPC callbacks
        if (RpcCallbacks.Count > 0 && stepData.ExtraActionCount > 0)
        {
            for (byte i = 0; i < stepData.ExtraActionCount; i++)
            {
                if (RpcCallbacks.Remove(extraActionPtr[i], out var callback))
                    callback();
            }
        }
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
        var seed = (uint)(DateTime.UtcNow.Ticks % uint.MaxValue);
        World.EntityManager.SetSingleton(new SharedRandom(){ Seed = seed, Random = Unity.Mathematics.Random.CreateFromIndex(seed)});
        var initSystemGroup = World.GetExistingSystemManaged<SurvivorWorldInitSystemGroup>();
        initSystemGroup.Enabled = true;
        initSystemGroup.Update();
        initSystemGroup.Enabled = false;
        
        // Spawn the terrain
        CompleteDependencies();
        if (GameType == 1)
        {
            var mapGen = Object.FindFirstObjectByType<MapGenMono>();
            if (mapGen) mapGen.Demo();
        }
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
        VisualsUpdated,
        WalletChanged,
        EnemyHealthChanged,
        PlayerDied,
        PlayerRevived,
        PlayerLevelProgress,
        PlayerLevelUp,
        PlayerDrawMapPoint,
    }
    
    
    [StructLayout(LayoutKind.Explicit, Pack = 4)]
    public readonly struct Data
    {
        [FieldOffset(0)] public readonly Type Type;
        [FieldOffset(4)] public readonly Entity Entity;
        [FieldOffset(12)] public readonly long _Data0;
        [FieldOffset(12)] public readonly long _Data1;
        
        [FieldOffset(12)] public readonly int Int0;
        [FieldOffset(16)] public readonly int Int1;
        [FieldOffset(20)] public readonly int Int2;
        [FieldOffset(24)] public readonly int Int3;

        [FieldOffset(16)] public readonly float3 Vec3;
        
        [FieldOffset(12)] public readonly Health Health;
        [FieldOffset(12)] public readonly Wallet Wallet;
        [FieldOffset(12)] public readonly PlayerLevel PlayerLevel;
        
        public static implicit operator Data((Type type, Entity entity) te) => new Data(te);
        
        public Data((Type type, Entity entity) te) : this(te.type, te.entity)
        {
        }
        public Data(Type type, Entity entity) : this()
        {
            Type = type;
            Entity = entity;
        }
        public Data(Type type, Entity entity, long data0) : this(type, entity)
        {
            _Data0 = data0;
        }
        public Data(Type type, Entity entity, int int0) : this(type, entity)
        {
            Int0 = int0;
        }
        public Data(Type type, Entity entity, int int0, int int1) : this(type, entity)
        {
            Int0 = int0;
            Int1 = int1;
        }
        public Data(Type type, Entity entity, int int0, float3 vec3) : this(type, entity)
        {
            Int0 = int0;
            Vec3 = vec3;
        }
        public Data(Type type, Entity entity, Wallet wallet) : this(type, entity)
        {
            Wallet = wallet;
        }
        public Data(Type type, Entity entity, Health health) : this(type, entity)
        {
            Health = health;
        }
        public Data(Type type, Entity entity, Health health, int change) : this(type, entity)
        {
            Health = health;
            Int2 = change;
        }
        public Data(Type type, Entity entity, PlayerLevel playerLevel) : this(type, entity)
        {
            PlayerLevel = playerLevel;
        }
    }

    static readonly SharedStatic<NativeQueue<Data>> s_EventQueue = SharedStatic<NativeQueue<Data>>.GetOrCreate<EventQueueKey>();

    private class EventQueueKey
    {
    }

    static bool m_Initialized;

    public static void Initialize()
    {
        if (m_Initialized)
        {
            throw new Exception($"{nameof(GameEvents)}.{nameof(Initialize)}() ran multiple times.");
        }

        m_Initialized = true;
        s_EventQueue.Data = new NativeQueue<Data>(Allocator.Persistent);
    }

    public static void Dispose()
    {
        s_EventQueue.Data.Dispose();
        m_Initialized = false;
    }

    public static void ExecuteEvents()
    {
        while (s_EventQueue.Data.TryDequeue(out var data))
        {
            switch (data.Type)
            {
                case InventoryChangedEnum:
                    OnInventoryChanged?.Invoke(data.Entity);
                    break;
                case PlayerHealthChangedEnum:
                    OnPlayerHealthChanged?.Invoke(data.Entity, data.Health);
                    break;
                case InteractableStartEnum:
                    OnInteractableStart?.Invoke(data.Entity);
                    break;
                case InteractableEndEnum:
                    OnInteractableEnd?.Invoke(data.Entity);
                    break;
                case VisualsUpdatedEnum:
                    OnVisualsUpdated?.Invoke(data.Entity);
                    break;
                case WalletChangedEnum:
                    OnWalletChanged?.Invoke(data.Entity, data.Wallet);
                    break;
                case EnemyHealthChangedEnum:
                    OnEnemyHealthChanged?.Invoke(data.Entity, data.Health, data.Int2);
                    break;
                case PlayerDiedEnum:
                    OnPlayerDied?.Invoke(data.Entity, data.Int0);
                    break;
                case PlayerRevivedEnum:
                    OnPlayerRevived?.Invoke(data.Entity, data.Int0);
                    break;
                case PlayerLevelProgressEnum:
                    OnPlayerLevelProgress?.Invoke(data.Entity, data.PlayerLevel);
                    break;
                case PlayerLevelUpEnum:
                    OnPlayerLevelUp?.Invoke(data.Entity, data.PlayerLevel);
                    break;
                case PlayerDrawMapPointEnum:
                    OnPlayerDrawMapPoint?.Invoke(data.Entity, data.Int0, data.Vec3);
                    break;
            }
        }
    }

    private const Type InventoryChangedEnum = Type.InventoryChanged;
    public delegate void OnInventoryChangedDel(Entity entity);
    public static event OnInventoryChangedDel OnInventoryChanged;
    public static void InventoryChanged(Entity entity)
    {
        s_EventQueue.Data.Enqueue(new (Type.InventoryChanged, entity));
    }

    private const Type PlayerHealthChangedEnum = Type.PlayerHealthChanged;
    public delegate void OnPlayerHealthChangedDel(Entity entity, Health health);
    public static event OnPlayerHealthChangedDel OnPlayerHealthChanged;
    public static void PlayerHealthChanged(Entity entity, Health health)
    {
        s_EventQueue.Data.Enqueue(new (Type.PlayerHealthChanged, entity, health));
    }

    private const Type InteractableStartEnum = Type.InteractableStart;
    public delegate void OnInteractableStartDel(Entity entity);
    public static event OnInteractableStartDel OnInteractableStart;
    public static void InteractableStart(Entity entity)
    {
        s_EventQueue.Data.Enqueue(new (Type.InteractableStart, entity));
    }

    private const Type InteractableEndEnum = Type.InteractableEnd;
    public delegate void OnInteractableEndDel(Entity entity);
    public static event OnInteractableEndDel OnInteractableEnd;
    public static void InteractableEnd(Entity entity)
    {
        s_EventQueue.Data.Enqueue(new (Type.InteractableEnd, entity));
    }

    private const Type VisualsUpdatedEnum = Type.VisualsUpdated;
    public delegate void OnVisualsUpdatedDel(Entity entity);
    public static event OnVisualsUpdatedDel OnVisualsUpdated;
    public static void VisualsUpdated(Entity entity)
    {
        s_EventQueue.Data.Enqueue(new (Type.VisualsUpdated, entity));
    }

    private const Type WalletChangedEnum = Type.WalletChanged;
    public delegate void OnWalletChangedDel(Entity entity, Wallet wallet);
    public static event OnWalletChangedDel OnWalletChanged;
    public static void WalletChanged(Entity entity, Wallet wallet)
    {
        s_EventQueue.Data.Enqueue(new (Type.WalletChanged, entity, wallet));
    }

    private const Type EnemyHealthChangedEnum = Type.EnemyHealthChanged;
    public delegate void OnEnemyHealthChangedDel(Entity entity, Health newHealth, int changeReceived);
    public static event OnEnemyHealthChangedDel OnEnemyHealthChanged;
    public static void EnemyHealthChanged(Entity entity, Health newHealth, int changeReceived)
    {
        s_EventQueue.Data.Enqueue(new (Type.EnemyHealthChanged, entity, newHealth, changeReceived));
    }

    private const Type PlayerDiedEnum = Type.PlayerDied;
    public delegate void OnPlayerDiedDel(Entity entity, int playerIndex);
    public static event OnPlayerDiedDel OnPlayerDied;
    public static void PlayerDied(Entity entity, int playerIndex)
    {
        s_EventQueue.Data.Enqueue(new (Type.PlayerDied, entity, playerIndex));
    }

    private const Type PlayerRevivedEnum = Type.PlayerRevived;
    public delegate void OnPlayerRevivedDel(Entity entity, int playerIndex);
    public static event OnPlayerRevivedDel OnPlayerRevived;
    public static void PlayerRevived(Entity entity, int playerIndex)
    {
        s_EventQueue.Data.Enqueue(new (Type.PlayerRevived, entity, playerIndex));
    }

    private const Type PlayerLevelProgressEnum = Type.PlayerLevelProgress;
    public delegate void OnPlayerLevelProgressDel(Entity entity, PlayerLevel playerLevel);
    public static event OnPlayerLevelProgressDel OnPlayerLevelProgress;
    public static void PlayerLevelProgress(Entity entity, PlayerLevel playerLevel)
    {
        s_EventQueue.Data.Enqueue(new (Type.PlayerLevelProgress, entity, playerLevel));
    }

    private const Type PlayerLevelUpEnum = Type.PlayerLevelUp;
    public delegate void OnPlayerLevelUpDel(Entity entity, PlayerLevel playerLevel);
    public static event OnPlayerLevelUpDel OnPlayerLevelUp;
    public static void PlayerLevelUp(Entity entity, PlayerLevel playerLevel)
    {
        s_EventQueue.Data.Enqueue(new (Type.PlayerLevelUp, entity, playerLevel));
    }
    
    private const Type PlayerDrawMapPointEnum = Type.PlayerDrawMapPoint;
    public delegate void OnPlayerDrawMapPointDel(Entity entity, int playerIndex, float3 pointPos);
    public static event OnPlayerDrawMapPointDel OnPlayerDrawMapPoint;
    public static void PlayerDrawMapPoint(Entity entity, int playerIndex, float3 pointPos)
    {
        s_EventQueue.Data.Enqueue(new (Type.PlayerDrawMapPoint, entity, playerIndex, pointPos));
    }

    public static bool TryGetSingleton<T>(out T o) where T : unmanaged, IComponentData
    {
        if (Game.ClientGame == null)
        {
            o = default;
            return false;
        }

        o = Game.ClientGame.World.EntityManager.GetSingleton<T>();
        return true;
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
    
    public static T GetComponent<T>(Entity entity) where T : unmanaged, IComponentData
    {
        return Game.ClientGame.World.EntityManager.GetComponentData<T>(entity);
    }
    public static bool HasComponent<T>(Entity entity) where T : unmanaged, IComponentData
    {
        if (Game.ClientGame == null || !Game.ClientGame.World.EntityManager.Exists(entity))
        {
            return false;
        }

        if (Game.ClientGame.World.EntityManager.HasComponent<T>(entity))
        {
            return true;
        }

        return false;
    }
    public static bool TryGetComponent2<T>(Entity entity, out T o) where T : unmanaged, IComponentData
    {
        if (Game.ClientGame == null || !Game.ClientGame.World.EntityManager.Exists(entity))
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