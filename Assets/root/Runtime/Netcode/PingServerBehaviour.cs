using System;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using BovineLabs.Core.Extensions;
using JetBrains.Annotations;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Networking.Transport;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Debug = UnityEngine.Debug;

public struct Client
{
    public NetworkConnection Connection;
    public StepInput InputBuffer;
    public bool HasInput;
    public bool RequestedSave;
    public bool Disabled;

    public Client(NetworkConnection connection)
    {
        this.Connection = connection;
        InputBuffer = default;
        HasInput = false;
        RequestedSave = false;
        Disabled = false;
    }
}

/// <summary>Component that will listen for ping connections and answer pings.</summary>
public unsafe class PingServerBehaviour : GameHostBehaviour
{
    public const int k_MaxPlayerCount = 16;

    public const byte CODE_SendStep = 0b0000_0000;
    public const byte CODE_SendSave = 0b0000_0001;
    public const byte CODE_SendId = 0b0000_0010;

    public static event Action OnLobbyHostStart;

    public override bool Idle => m_Idle;
    public string RelayJoinCode;
    public Lobby Lobby;

    private BiggerDriver m_ServerDriver;
    private NativeArray<Client> m_ServerConnections;
    private NativeReference<FullStepData> m_ServerToClient;
    private NativeQueue<GameRpc> m_SpecialActionQueue;
    private NativeList<GameRpc> m_SpecialActionList;

    private NativeArray<byte> m_SaveBuffer;

    // Handle to the job chain of the ping server. We need to keep this around so that we can
    // schedule the jobs in one execution of Update and complete it in the next.
    private JobHandle m_ServerJobHandle;
    private bool m_Idle;

    private void Start()
    {
        m_ServerConnections = new NativeArray<Client>(k_MaxPlayerCount, Allocator.Persistent);
        m_ServerToClient = new NativeReference<FullStepData>(Allocator.Persistent);
        m_SaveBuffer = new NativeArray<byte>(PingClientBehaviour.k_MaxSaveSize, Allocator.Persistent);
        m_SpecialActionQueue = new NativeQueue<GameRpc>(Allocator.Persistent);
        m_SpecialActionList = new NativeList<GameRpc>(4, Allocator.Persistent); // This can be any size
    }

    private void OnDestroy()
    {
        if (Lobby != null)
        {
            LobbyService.Instance.RemovePlayerAsync(Lobby.Id, AuthenticationService.Instance.PlayerId);
            Lobby = null;
        }

        Game?.Dispose();

        if (m_ServerDriver.IsCreated)
        {
            // All jobs must be completed before we can dispose of the data they use.
            m_ServerJobHandle.Complete();
            m_ServerDriver.Dispose();
        }

        m_ServerConnections.Dispose();
        m_ServerToClient.Dispose();
        m_SaveBuffer.Dispose();
        m_SpecialActionQueue.Dispose();
        m_SpecialActionList.Dispose();
    }

    /// <summary>Start establishing a connection to the server and listening for connections.</summary>
    /// <returns>Enumerator for a coroutine.</returns>
    public IEnumerator Connect(string lobbyName, Action OnSuccess = null, Action OnFailure = null)
    {
        var signInTask = GameLaunch.SignIn();
        while (!signInTask.IsCompleted)
            yield return null;
        if (signInTask.IsFaulted)
        {
            Debug.LogError($"Failed to sign in: {signInTask.Exception}");
            OnFailure?.Invoke();
            yield break;
        }

        var allocationTask = RelayService.Instance.CreateAllocationAsync(k_MaxPlayerCount, "australia-southeast1");
        while (!allocationTask.IsCompleted)
            yield return null;
        if (allocationTask.IsFaulted)
        {
            Debug.LogError($"Failed to create Relay allocation: {allocationTask.Exception}");
            OnFailure?.Invoke();
            yield break;
        }

        var allocation = allocationTask.Result;

        var joinCodeTask = RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        while (!joinCodeTask.IsCompleted)
            yield return null;
        if (joinCodeTask.IsFaulted)
        {
            Debug.LogError("Failed to request join code for allocation: " + joinCodeTask.Exception);
            OnFailure?.Invoke();
            yield break;
        }

        RelayJoinCode = joinCodeTask.Result;
        JavascriptHook.SetUrlArg("lobby", joinCodeTask.Result);

        // Do this on another thread because it kills performance
        var relayServerData = allocation.ToRelayServerData("wss");
        var settings = Netcode.NetworkSettings(ref relayServerData);
        settings = settings.WithNetworkConfigParameters(disconnectTimeoutMS: 3000);

        m_ServerDriver = new BiggerDriver(NetworkDriver.Create(new WebSocketNetworkInterface(), settings));

        // NetworkDriver expects to be bound to something before listening for connections, but
        // for Relay it really doesn't matter what we bound to. AnyIpv4 is as good as any.
        if (m_ServerDriver.Driver.Bind(NetworkEndpoint.AnyIpv4) < 0)
        {
            Debug.LogError("Failed to bind the NetworkDriver.");
            OnFailure?.Invoke();
            yield break;
        }

        if (m_ServerDriver.Driver.Listen() < 0)
        {
            Debug.LogError("Failed to start listening for connections.");
            OnFailure?.Invoke();
            yield break;
        }

        Debug.Log("Server is now listening for connections.");
        
        var createOptions = new CreateLobbyOptions
        {
            IsPrivate = false,
            Data = new()
            {
                ["RelayJoinCode"] = new DataObject(DataObject.VisibilityOptions.Public, value: RelayJoinCode),
            }
        };
        var lobbyHostTask = LobbyService.Instance.CreateLobbyAsync(lobbyName, PingServerBehaviour.k_MaxPlayerCount, createOptions);
        while (!lobbyHostTask.IsCompleted)
            yield return null;
        if (lobbyHostTask.IsFaulted)
        {
            Debug.LogError("Failed to host lobby: " + lobbyHostTask.Exception);
            OnFailure?.Invoke();
            yield break;
        }
        
        Lobby = lobbyHostTask.Result;
        Debug.Log($"Hosting lobby {Lobby.Name} with id {Lobby.Id}");
        
        
        yield return new WaitUntil(() => Game.IsReady);
        
        m_Idle = true;
        Debug.Log($"{this} entered idle state...");
        
        OnSuccess?.Invoke();
        OnLobbyHostStart?.Invoke();
        
    }

    // Job to clean up old connections and accept new ones.
    [BurstCompile]
    private struct ConnectionsRelayUpdateJob : IJob
    {
        public BiggerDriver Driver;
        public NativeArray<Client> Connections;
        public NativeQueue<GameRpc>.ParallelWriter SpecialActionQueue;

        public void Execute()
        {
            // Accept all new connections.
            NetworkConnection connection;
            while ((connection = Driver.Driver.Accept()) != default)
            {
                // Find and use a new slot.
                for (byte i = 0; i < Connections.Length; i++)
                {
                    if (Connections[i].Connection.IsCreated || Connections[i].Disabled) continue;

                    Debug.Log($"Got new client {connection} at index {i}");
                    Connections[i] = new Client(connection) { RequestedSave = true };
                    SpecialActionQueue.Enqueue(GameRpc.PlayerJoin(i, 0));
                    break;
                }
            }
        }
    }

    // Job to process events from all connections in parallel.
    [BurstCompile]
    private unsafe struct ConnectionsRelayEventsJobs : IJobParallelFor
    {
        public NetworkDriver.Concurrent Driver;
        public NativeArray<Client> Connections;
        public NativeQueue<GameRpc>.ParallelWriter SpecialActionQueue;

        [NativeDisableContainerSafetyRestriction]
        public NativeArray<GameRpc> SpecialActions;

        [NativeDisableContainerSafetyRestriction]
        public NativeReference<FullStepData> ServerToClient;

        public unsafe void Execute(int i)
        {
            var connection = Connections[i];
            if (!connection.Connection.IsCreated) return;

            NetworkEvent.Type eventType;
            while ((eventType = Driver.PopEventForConnection(connection.Connection, out var reader)) != NetworkEvent.Type.Empty)
            {
                if (eventType == NetworkEvent.Type.Data)
                {
                    switch (reader.ReadByte())
                    {
                        case PingClientBehaviour.CODE_SendInput:
                            connection.InputBuffer = StepInput.Read(ref reader);
                            connection.HasInput = true;
                            break;
                        case PingClientBehaviour.CODE_RequestSave:
                            Debug.Log($"Save requested...");
                            connection.RequestedSave = true;
                            break;
                        case PingClientBehaviour.CODE_SendRpc:
                            var rpc = GameRpc.Read(ref reader);
                            if (!rpc.IsValidClientRpc)
                                Debug.Log($"{connection} sent illegal RPC: {rpc} ({rpc.Type}) ({(byte)rpc.Type})");
                            else if (rpc.PlayerId != i)
                                Debug.Log($"{connection} sent RPC for different player: {rpc}");
                            else
                            {
                                SpecialActionQueue.Enqueue(rpc);
                                Debug.Log($"Received RPC: {rpc}");
                            }

                            break;
                    }

                    Connections[i] = connection;
                }
                else if (eventType == NetworkEvent.Type.Disconnect)
                {
                    SpecialActionQueue.Enqueue(new GameRpc() { Type = GameRpc.Code.PlayerLeave, PlayerId = (byte)i });
                    Connections[i] = default;
                }
            }

            if (ServerToClient.IsCreated)
            {
                var result = Driver.BeginSend(connection.Connection, out var writer);
                if (result < 0)
                {
                    Debug.LogError($"Couldn't send ping answer (error code {result}).");
                    return;
                }

                writer.WriteByte(PingServerBehaviour.CODE_SendStep);
                ServerToClient.Value.Write(ref writer, (byte)SpecialActions.Length, (GameRpc*)SpecialActions.GetUnsafePtr());

                result = Driver.EndSend(writer);
                if (result < 0)
                {
                    Debug.LogError($"Couldn't send ping answer (error code {result}).");
                    return;
                }
            }
        }
    }

    public void SetStepInput(int connectionId, StepInput input)
    {
        if (connectionId < 0 || connectionId >= m_ServerConnections.Length)
        {
            Debug.LogError($"Invalid connection ID: {connectionId}");
            return;
        }

        m_ServerJobHandle.Complete();

        var connection = m_ServerConnections[connectionId];
        connection.InputBuffer = input;
        m_ServerConnections[connectionId] = connection;
    }
    
    Stopwatch m_LobbyHeartbestSw;

    private void Update()
    {
        if (Lobby != null)
        {
            if (m_LobbyHeartbestSw == null) m_LobbyHeartbestSw = Stopwatch.StartNew();
            if (m_LobbyHeartbestSw.Elapsed.Seconds > 10)
            {
                m_LobbyHeartbestSw.Restart();
                LobbyService.Instance.SendHeartbeatPingAsync(Lobby.Id);
            }
        }
        
        if (m_ServerDriver.IsCreated)
        {
        
            // First, complete the previously-scheduled job chain.
            m_ServerJobHandle.Complete();

            SaveLoop:
            var saveState = Game.SaveState;
            for (byte i = 0; i < m_ServerConnections.Length; i++)
                if (m_ServerConnections[i].RequestedSave)
                {
                    if (saveState == SaveState.Idle)
                    {
                        Game.InitSave();
                        goto SaveLoop;
                    }

                    if (saveState == SaveState.Saving)
                    {
                        Debug.Log("... saving...");
                        Game.Update_NoLogic();
                        goto SaveLoop;
                    }

                    if (saveState == SaveState.Ready)
                    {
                        // Send
                        var con = m_ServerConnections[i];
                        SendSaveToConnection(m_ServerDriver, con);
                        SendIdToConnection(m_ServerDriver, con, i);
                        con.RequestedSave = false;
                        m_ServerConnections[i] = con;
                    }
                }

            if (saveState == SaveState.Ready) Game.CleanSave();

            // Create the jobs first.
            var updateJob = new ConnectionsRelayUpdateJob
            {
                Driver = m_ServerDriver,
                Connections = m_ServerConnections,
                SpecialActionQueue = m_SpecialActionQueue.AsParallelWriter()
            };
            var eventsJobs = new ConnectionsRelayEventsJobs
            {
                Driver = m_ServerDriver.Driver.ToConcurrent(),
                Connections = m_ServerConnections,
                SpecialActionQueue = m_SpecialActionQueue.AsParallelWriter()
            };

            if (!Game.IsReady)
            {
                Game.Update_NoLogic();
            }
            else
            {
                if (m_ServerConnections.Length > 0 || Game.PlayerIndex != -1)
                {
                    if (Game.PlayerIndex != -1)
                    {
                        var old = m_ServerConnections[Game.PlayerIndex];
                        old.InputBuffer.Collect(Camera.main);
                        m_ServerConnections[Game.PlayerIndex] = old;
                    }

                    if (Game.CanStep())
                    {
                        eventsJobs.ServerToClient = m_ServerToClient;

                        // Iterate index + read inputs
                        var newStepData = new FullStepData(Game.Step + 1, m_ServerConnections);

                        // If we're connected, clear our buffer.
                        if (Game.PlayerIndex != -1)
                        {
                            var old = m_ServerConnections[Game.PlayerIndex];
                            old.InputBuffer = default;
                            m_ServerConnections[Game.PlayerIndex] = old;
                        }
                        
                        // Insert local special actions
                        while (Game.RpcSendBuffer.TryDequeue(out var localRpc))
                            m_SpecialActionQueue.Enqueue(localRpc);

                        // Load special actions
                        m_SpecialActionList.Clear();
                        while (m_SpecialActionList.Length < PingClientBehaviour.k_MaxSpecialActionCount && m_SpecialActionQueue.TryDequeue(out var act))
                            m_SpecialActionList.Add(act);
                        eventsJobs.SpecialActions = m_SpecialActionList.AsArray();
                        newStepData.ExtraActionCount = (byte)eventsJobs.SpecialActions.Length;

                        // Update server sim
                        Game.ApplyStepData(newStepData, (GameRpc*)eventsJobs.SpecialActions.GetUnsafePtr());
                        m_ServerToClient.Value = newStepData;
                        NetworkPing.ServerPingTimes.Data.Add((DateTime.Now, (int)newStepData.Step));
                    }
                    else
                    {
                        Game.ApplyRender();
                    }
                }
            }

            // Schedule the job chain.
            m_ServerJobHandle = m_ServerDriver.Driver.ScheduleUpdate();
            m_ServerJobHandle = updateJob.Schedule(m_ServerJobHandle);
            m_ServerJobHandle = eventsJobs.Schedule(m_ServerConnections.Length, 1, m_ServerJobHandle);
        }
    }

    public unsafe void SendSaveToConnection(BiggerDriver Driver, Client Connection)
    {
        Debug.Log($"... sending save...");

        var result = Driver.Driver.BeginSend(Driver.LargePipeline, Connection.Connection, out var writer);
        if (result < 0)
        {
            Debug.LogError($"Couldn't send ping answer (error code {result}).");
            return;
        }

        writer.WriteByte(PingServerBehaviour.CODE_SendSave);
        Game.SendSave(ref writer);
        result = Driver.Driver.EndSend(writer);
        if (result < 0)
        {
            Debug.LogError($"Couldn't send ping answer (error code {result}).");
            return;
        }
    }

    public void SendIdToConnection(BiggerDriver Driver, Client Connection, int index)
    {
        Debug.Log($"... sending Id...");

        var result = Driver.Driver.BeginSend(Driver.LargePipeline, Connection.Connection, out var writer);
        if (result < 0)
        {
            Debug.LogError($"Couldn't send ping answer (error code {result}).");
            return;
        }

        writer.WriteByte(PingServerBehaviour.CODE_SendId);
        GenericMessage.Id(index).Write(ref writer);

        result = Driver.Driver.EndSend(writer);
        if (result < 0)
        {
            Debug.LogError($"Couldn't send ping answer (error code {result}).");
            return;
        }
    }

    /// <summary>
    /// Locks out a slot and inserts a new local player
    /// </summary>
    public void AddLocalPlayer()
    {
        m_ServerJobHandle.Complete();
        
        for (byte i = 0; i < m_ServerConnections.Length; i++)
        {
            if (!m_ServerConnections[i].Connection.IsCreated && !m_ServerConnections[i].Disabled)
            {
                Game.PlayerIndex = i;

                m_ServerConnections.ElementAt(i).Disabled = true;
                m_SpecialActionQueue.Enqueue(GameRpc.PlayerJoin(i, 0));
                return;
            }
        }

        Debug.LogError("No free slots available for a new player.");
    }
    
    /// <summary>
    /// Locks out a slot for an existing local player to take up
    /// </summary>
    public void RegisterLocalPlayer()
    {
        m_ServerJobHandle.Complete();
        
        for (byte i = 0; i < m_ServerConnections.Length; i++)
        {
            if (!m_ServerConnections[i].Connection.IsCreated && !m_ServerConnections[i].Disabled)
            {
                Game.PlayerIndex = i;

                m_ServerConnections.ElementAt(i).Disabled = true;
                return;
            }
        }
    }
}