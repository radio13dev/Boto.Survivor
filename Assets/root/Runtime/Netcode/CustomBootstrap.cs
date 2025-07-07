using System;
using System.Collections;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.SceneManagement;

[UnityEngine.Scripting.Preserve]
public class Bootstrap : ClientServerBootstrap
{
    const ushort k_NetworkPort = 7979;
    
    public override bool Initialize(string defaultWorldName)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        AutoConnectPort = 0;
        var ep = NetworkEndpoint.LoopbackIpv4;//NetworkEndpoint.Parse(Address.text, ParsePortOrDefault(Port.text));
        ep.Port = 7979;
        Debug.Log($"[ConnectToServer] Called on '{ep.Address}:{ep.Port}'.");
        
        NetworkStreamReceiveSystem.DriverConstructor = new MyCustomDriverConstructor()
        {
            connectWithSsl = false,
            hostname = ep.Address
        };
        
        var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
        DestroyLocalSimulationWorld();

        if (World.DefaultGameObjectInjectionWorld == null)
            World.DefaultGameObjectInjectionWorld = client;
        //var sceneName = GetAndSaveSceneSelection();
        //SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

        {
            using var drvQuery = client.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(client.EntityManager, ep);
        }
        return true;
#else
        Debug.Log($"[StartClientServer] Called with '{defaultWorldName}'.");
        if (ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.ClientAndServer)
        {
            Debug.LogError($"Creating client/server worlds is not allowed if playmode is set to {ClientServerBootstrap.RequestedPlayType}");
            return false;
        }
        
        NetworkStreamReceiveSystem.DriverConstructor = new MyCustomDriverConstructor()
        {
            serverCertificate = "",
            serverPrivateKey = "",
            connectWithSsl = false,
            hostname = NetworkEndpoint.LoopbackIpv4.Address
        };

        var server = ClientServerBootstrap.CreateServerWorld("ServerWorld");
        var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");

        //Destroy the local simulation world to avoid the game scene to be loaded into it
        //This prevent rendering (rendering from multiple world with presentation is not greatly supported)
        //and other issues.
        DestroyLocalSimulationWorld();
        if (World.DefaultGameObjectInjectionWorld == null)
            World.DefaultGameObjectInjectionWorld = server;

        var port = ParsePortOrDefault("7979");

        NetworkEndpoint ep = NetworkEndpoint.AnyIpv4.WithPort(port);
        {
            using var drvQuery = server.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.RequireConnectionApproval = false;//sceneName.Contains("ConnectionApproval", StringComparison.OrdinalIgnoreCase);
            drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(ep);
        }

        ep = NetworkEndpoint.LoopbackIpv4.WithPort(port);
        {
            using var drvQuery = client.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(client.EntityManager, ep);
        }
        return true;
#endif
    }
    
    /// <summary>
    /// Stores the old name of the local world (create by initial bootstrap).
    /// It is reused later when the local world is created when coming back from game to the menu.
    /// </summary>
    internal static string OldFrontendWorldName = string.Empty;
    protected void DestroyLocalSimulationWorld()
    {
        foreach (var world in World.All)
        {
            if (world.Flags == WorldFlags.Game)
            {
                OldFrontendWorldName = world.Name;
                world.Dispose();
                break;
            }
        }
    }
    
    // Tries to parse a port, returns true if successful, otherwise false
    // The port will be set to whatever is parsed, otherwise the default port of k_NetworkPort
    private UInt16 ParsePortOrDefault(string s)
    {
        if (!UInt16.TryParse(s, out var port))
        {
            Debug.LogWarning($"Unable to parse port, using default port {k_NetworkPort}");
            return k_NetworkPort;
        }

        return port;
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct ClientInGame : ISystem
{
    private bool m_HasRegisteredSmoothingAction;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameManager.Resources>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var cmdBuffer = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (_, entity) in SystemAPI.Query<RefRO<NetworkId>>().WithNone<NetworkStreamInGame>()
                     .WithEntityAccess())
        {
            Debug.Log($"[ClientInGame][{state.WorldUnmanaged.Name}] Setting up network streaming.");
            cmdBuffer.AddComponent<NetworkStreamInGame>(entity);
        }

        cmdBuffer.Playback(state.EntityManager);
    }
}


public struct UserInitialized : IComponentData
{
}

public struct NewUser : IRpcCommand
{
    public int UserData;
}

/// <summary>
///     Convenience: This allows us to trivially fetch the connection entity associated with
///     this player character controller entity.
/// </summary>
public struct ConnectionOwner : IComponentData
{
    public Entity Entity;
}

[BurstCompile]
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct SpawnPlayerSystem : ISystem
{
    private EntityQuery m_NewPlayersQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // Must wait for the spawner entity scene to be streamed in,
        // which is most likely instantaneous in this sample (but good to be sure).
        state.RequireForUpdate<GameManager.Resources>();
        m_NewPlayersQuery = SystemAPI.QueryBuilder().WithAll<NetworkId>().WithNone<UserInitialized>().Build();
        state.RequireForUpdate(m_NewPlayersQuery);
        Debug.Log($"[SpawnPlayerSystem][{state.WorldUnmanaged.Name}] Setup.");
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var resources = SystemAPI.GetSingleton<GameManager.Resources>();

        // Iterate through all connections events raised by netcode,
        // and if they're new joiners, spawn a player character controller for them:
        var connectionEntities = m_NewPlayersQuery.ToEntityArray(Allocator.Temp);

        var networkIds = m_NewPlayersQuery.ToComponentDataArray<NetworkId>(Allocator.Temp);
        for (var i = 0; i < connectionEntities.Length; i++)
        {
            var networkId = networkIds[i];
            var connectionEntity = connectionEntities[i];
            var survivorE = state.EntityManager.Instantiate(resources.SurvivorTemplate);
            Debug.Log(
                $"[SpawnPlayerSystem][{state.WorldUnmanaged.Name}] Spawning player CC '{survivorE.ToFixedString()}' (from prefab 'PlayerTrain') for {networkId.ToFixedString()}.");

            // The network ID owner must be set on the spawned ghost.
            // Doing so gives said client the authority to raise inputs for (i.e. to control) this ghost.
            state.EntityManager.SetComponentData(survivorE, new GhostOwner { NetworkId = networkId.Value });

            // This is to support thin client players.
            // You don't normally need to do this, as it's typically easier to simply enable the AutoCommandTarget
            // (via the GhostAuthoringComponent).
            // See the ThinClients sample for more details.
            state.EntityManager.SetComponentData(connectionEntity, new CommandTarget { targetEntity = survivorE });

            // Add the player to the linked entity group on the connection, so it is destroyed
            // automatically on disconnect (i.e. it's destroyed along with the connection entity,
            // when the connection entity is destroyed).
            state.EntityManager.GetBuffer<LinkedEntityGroup>(connectionEntity).Add(new LinkedEntityGroup { Value = survivorE });

            // This is a convenience: It allows us to trivially fetch the connection entity associated with
            // this player character controller entity.
            Debug.Log($"[SpawnPlayerSystem][{state.WorldUnmanaged.Name}] Setting up network streaming.");
            state.EntityManager.AddComponentData(survivorE, new ConnectionOwner { Entity = connectionEntity });

            // Mark that this connection has had a player spawned for it, so we won't process it again:
            state.EntityManager.AddComponent<UserInitialized>(connectionEntity);
        }
    }
}