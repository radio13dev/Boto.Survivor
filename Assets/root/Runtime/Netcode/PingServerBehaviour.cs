using System;
using System.Collections;
using System.Threading.Tasks;
using BovineLabs.Core.Extensions;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Networking.Transport;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

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
public unsafe class PingServerBehaviour : MonoBehaviour, IGameBehaviour
{
    public const int k_MaxPlayerCount = 4;

    public const byte CODE_SendStep = 0b0000_0000;
    public const byte CODE_SendSave = 0b0000_0001;
    public const byte CODE_SendId = 0b0000_0010;

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
    internal Game m_Game;

    private void Start()
    {
        m_ServerConnections = new NativeArray<Client>(k_MaxPlayerCount, Allocator.Persistent);
        m_ServerToClient = new NativeReference<FullStepData>(Allocator.Persistent);
        m_SaveBuffer = new NativeArray<byte>(PingClientBehaviour.k_MaxSaveSize, Allocator.Persistent);
        m_SpecialActionQueue = new NativeQueue<SpecialLockstepActions>(Allocator.Persistent);
        m_SpecialActionList = new NativeList<SpecialLockstepActions>(4, Allocator.Persistent); // This can be any size
    }

    public void SetupGame()
    {
        m_Game = new Game(false);
        m_Game.LoadScenes();
    }

    private void OnDestroy()
    {
        m_Game?.Dispose();

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
    public IEnumerator Connect(Action OnSuccess, Action OnFailure)
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

        JoinCode = joinCodeTask.Result;
        JavascriptHook.SetUrlArg("lobby", joinCodeTask.Result);

        // Do this on another thread because it kills performance
        var relayServerData = allocation.ToRelayServerData("wss");
        var settings = Netcode.NetworkSettings(ref relayServerData);

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
        OnSuccess?.Invoke();
    }

    public string JoinCode;

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
                    if (Connections[i].Connection.IsCreated || Connections[i].Disabled) continue;

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

            SaveLoop:
            var saveState = m_Game.SaveState;
            for (int i = 0; i < m_ServerConnections.Length; i++)
                if (m_ServerConnections[i].RequestedSave)
                {
                    if (saveState == SaveState.Idle)
                    {
                        m_Game.InitSave();
                        m_Game.Update_NoLogic();
                        goto SaveLoop;
                    }

                    if (saveState == SaveState.Saving)
                    {
                        Debug.Log("... saving...");
                        m_Game.Update_NoLogic();
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

            if (saveState == SaveState.Ready) m_Game.CleanSave();

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
                m_Game.Update_NoLogic();
            }
            else if (m_ServerConnections.Length > 0 || m_Game.PlayerIndex != -1)
            {
                if (m_Game.PlayerIndex != -1)
                {
                    var old = m_ServerConnections[m_Game.PlayerIndex];
                    old.InputBuffer.Collect(Camera.main);
                    m_ServerConnections[m_Game.PlayerIndex] = old;
                }

                m_CumulativeTime += Time.deltaTime;
                if (m_CumulativeTime >= Game.k_ServerPingFrequency && ClientDesyncDebugger.CanExecuteStep(m_ServerToClient.Value.Step + 1))
                {
                    m_CumulativeTime = math.min(m_CumulativeTime - Game.k_ServerPingFrequency, Game.k_ServerPingFrequency);

                    eventsJobs.ServerToClient = m_ServerToClient;

                    // Iterate index + read inputs
                    var newStepData = new FullStepData(m_Game.Step + 1, m_ServerConnections);

                    // If we're connected, clear our buffer.
                    if (m_Game.PlayerIndex != -1)
                    {
                        var old = m_ServerConnections[m_Game.PlayerIndex];
                        old.InputBuffer = default;
                        m_ServerConnections[m_Game.PlayerIndex] = old;
                    }

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
                else
                {
                    m_Game.ApplyRender(math.clamp(m_CumulativeTime / Game.k_ServerPingFrequency, 0, 1));
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

    public void AddLocalPlayer()
    {
        for (int i = 0; i < m_ServerConnections.Length; i++)
        {
            if (!m_ServerConnections[i].Connection.IsCreated && !m_ServerConnections[i].Disabled)
            {
                m_SpecialActionQueue.Enqueue(SpecialLockstepActions.PlayerJoin(i));
                m_Game.PlayerIndex = i;
                
                m_ServerConnections.ElementAt(i).Disabled = true;
                return;
            }
        }

        Debug.LogError("No free slots available for a new player.");
    }
}