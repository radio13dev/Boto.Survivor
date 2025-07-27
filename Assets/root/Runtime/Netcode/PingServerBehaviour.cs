using System;
using System.Collections;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Networking.Transport;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public struct Client
{
    public NetworkConnection Connection;
    public StepInput InputBuffer;
    public bool HasInput;
    public bool RequestedSave;

    public Client(NetworkConnection connection)
    {
        this.Connection = connection;
        InputBuffer = default;
        HasInput = false;
        RequestedSave = false;
    }
}

/// <summary>Component that will listen for ping connections and answer pings.</summary>
public unsafe class PingServerBehaviour : MonoBehaviour
{
    public const int k_MaxPlayerCount = 4;

    public const byte CODE_SendStep = 0b0000_0000;
    public const byte CODE_SendSave = 0b0000_0001;
    public const byte CODE_SendId = 0b0000_0010;

    /// <summary>UI component on which to set the join code.</summary>
    public PingUIBehaviour PingUI;

    private BiggerDriver m_ServerDriver;
    private NativeArray<Client> m_ServerConnections;
    private NativeReference<FullStepData> m_ServerToClient;
    private NativeQueue<SpecialLockstepActions> m_SpecialActionQueue;
    private NativeList<SpecialLockstepActions> m_SpecialActionList;

    private NativeArray<byte> m_SaveBuffer;

    private float m_CumulativeTime;

    // Handle to the job chain of the ping server. We need to keep this around so that we can
    // schedule the jobs in one execution of Update and complete it in the next.
    private JobHandle m_ServerJobHandle;
    private Game m_Game;
    public Game Game => m_Game;

    private void Start()
    {
        m_ServerConnections = new NativeArray<Client>(k_MaxPlayerCount, Allocator.Persistent);
        m_ServerToClient = new NativeReference<FullStepData>(Allocator.Persistent);
        m_SaveBuffer = new NativeArray<byte>(PingClientBehaviour.k_MaxSaveSize, Allocator.Persistent);
        m_SpecialActionQueue = new NativeQueue<SpecialLockstepActions>(Allocator.Persistent);
        m_SpecialActionList = new NativeList<SpecialLockstepActions>(4, Allocator.Persistent); // This can be any size
        m_Game = new Game(false);
        m_Game.LoadScenes();
    }

    private void OnDestroy()
    {
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
    public IEnumerator Connect()
    {
        var allocationTask = RelayService.Instance.CreateAllocationAsync(5, "australia-southeast1");
        while (!allocationTask.IsCompleted)
            yield return null;

        if (allocationTask.IsFaulted)
        {
            Debug.LogError("Failed to create Relay allocation.");
            yield break;
        }

        var allocation = allocationTask.Result;

        var joinCodeTask = RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        while (!joinCodeTask.IsCompleted)
            yield return null;

        if (joinCodeTask.IsFaulted)
        {
            Debug.LogError("Failed to request join code for allocation.");
            yield break;
        }

        PingUI.JoinCode = joinCodeTask.Result;
        JavascriptHook.SetUrlArg("lobby", joinCodeTask.Result);

        var relayServerData = allocation.ToRelayServerData("wss");
        var settings = Netcode.NetworkSettings(ref relayServerData);

        m_ServerDriver = new BiggerDriver(NetworkDriver.Create(new WebSocketNetworkInterface(), settings));

        // NetworkDriver expects to be bound to something before listening for connections, but
        // for Relay it really doesn't matter what we bound to. AnyIpv4 is as good as any.
        if (m_ServerDriver.Driver.Bind(NetworkEndpoint.AnyIpv4) < 0)
        {
            Debug.LogError("Failed to bind the NetworkDriver.");
            yield break;
        }

        if (m_ServerDriver.Driver.Listen() < 0)
        {
            Debug.LogError("Failed to start listening for connections.");
            yield break;
        }

        Debug.Log("Server is now listening for connections.");
    }

    // Job to clean up old connections and accept new ones.
    [BurstCompile]
    private struct ConnectionsRelayUpdateJob : IJob
    {
        public BiggerDriver Driver;
        public NativeArray<Client> Connections;
        public NativeQueue<SpecialLockstepActions>.ParallelWriter SpecialActionQueue;

        public void Execute()
        {
            // Accept all new connections.
            NetworkConnection connection;
            while ((connection = Driver.Driver.Accept()) != default)
            {
                // Find and use a new slot.
                for (int i = 0; i < Connections.Length; i++)
                {
                    if (Connections[i].Connection.IsCreated) continue;

                    Debug.Log($"Got new client {connection} at index {i}");
                    Connections[i] = new Client(connection) { RequestedSave = true };
                    SpecialActionQueue.Enqueue(SpecialLockstepActions.PlayerJoin(i));
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
        public NativeQueue<SpecialLockstepActions>.ParallelWriter SpecialActionQueue;

        [NativeDisableContainerSafetyRestriction]
        public NativeArray<SpecialLockstepActions> SpecialActions;

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
                            var rpc = SpecialLockstepActions.Read(ref reader);
                            if (!rpc.IsValidClientRpc)
                                Debug.Log($"{connection} sent illegal RPC: {rpc.Type} {rpc.Data} {rpc.Extension}");
                            else if (rpc.Data != i)
                                Debug.Log($"{connection} sent RPC for different player: {rpc.Type} {rpc.Data} {rpc.Extension}");
                            else
                            {
                                SpecialActionQueue.Enqueue(rpc);
                                Debug.Log($"Received RPC: {rpc.Type} {rpc.Data} {rpc.Extension}");
                            }

                            break;
                    }

                    Connections[i] = connection;
                }
                else if (eventType == NetworkEvent.Type.Disconnect)
                {
                    SpecialActionQueue.Enqueue(SpecialLockstepActions.PlayerLeave(i));
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
                ServerToClient.Value.Write(ref writer, (byte)SpecialActions.Length, (SpecialLockstepActions*)SpecialActions.GetUnsafePtr());

                result = Driver.EndSend(writer);
                if (result < 0)
                {
                    Debug.LogError($"Couldn't send ping answer (error code {result}).");
                    return;
                }
            }
        }
    }

    private void Update()
    {
        if (m_ServerDriver.IsCreated)
        {
            // First, complete the previously-scheduled job chain.
            m_ServerJobHandle.Complete();

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

            if (!m_Game.IsReady)
            {
                m_Game.World.Update();
            }
            else if (m_ServerConnections.Length > 0)
            {
                m_CumulativeTime += Time.deltaTime;
                if (m_CumulativeTime >= Game.k_ServerPingFrequency)
                {
                    m_CumulativeTime -= Game.k_ServerPingFrequency;

                    eventsJobs.ServerToClient = m_ServerToClient;

                    // Iterate index + read inputs
                    var old = m_ServerToClient.Value;
                    var newStepData = new FullStepData(old.Step + 1, m_ServerConnections);

                    // Load special actions
                    m_SpecialActionList.Clear();
                    while (m_SpecialActionList.Length < PingClientBehaviour.k_MaxSpecialActionCount && m_SpecialActionQueue.TryDequeue(out var act))
                        m_SpecialActionList.Add(act);
                    eventsJobs.SpecialActions = m_SpecialActionList.AsArray();
                    newStepData.ExtraActionCount = (byte)eventsJobs.SpecialActions.Length;

                    // Update server sim
                    m_Game.ApplyStepData(newStepData, (SpecialLockstepActions*)eventsJobs.SpecialActions.GetUnsafePtr());
                    m_ServerToClient.Value = newStepData;
                    NetworkPing.ServerPingTimes.Data.Add((DateTime.Now, (int)newStepData.Step));
                }
            }

            var saveState = m_Game.SaveState;
            for (int i = 0; i < m_ServerConnections.Length; i++)
                if (m_ServerConnections[i].RequestedSave)
                {
                    if (saveState == SaveState.Idle)
                    {
                        m_Game.InitSave();
                        continue;
                    }

                    if (saveState == SaveState.Saving)
                    {
                        Debug.Log("... saving...");
                        continue; // Wait
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

            if (saveState == SaveState.Ready) m_Game.CleanSave();

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
        m_Game.SendSave(ref writer);
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
}