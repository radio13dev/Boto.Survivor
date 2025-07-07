using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

[UnityEngine.Scripting.Preserve]
public class Bootstrap : ClientServerBootstrap
{
    public override bool Initialize(string defaultWorldName)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        AutoConnectPort = 7979;
        CreateClientWorld("Webgl Client");
        return true;
#else
        // If the user added an OverrideDefaultNetcodeBootstrap MonoBehaviour to their active scene,
        // or disabled Bootstrapping project-wide, this is respected here.
        if (!DetermineIfBootstrappingEnabled())
        {
            Debug.Log($"Bootstrapping disabled");
            return false;
        }
            
        Debug.Log($"Bootstrapping enabled");
        AutoConnectPort = 7979;
        CreateDefaultClientServerWorlds();
        return true;
#endif
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct ClientInGame : ISystem
{
    private bool m_HasRegisteredSmoothingAction;

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