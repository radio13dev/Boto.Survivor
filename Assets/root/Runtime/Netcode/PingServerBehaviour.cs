using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Jobs;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEditor;

namespace Unity.Networking.Transport.Samples
{
    [StructLayout(layoutKind: LayoutKind.Sequential)]
    public unsafe struct ServerToClient
    {
        public long Step;
        public byte Data0;
        public byte Data1;
        public byte Data2;
        public byte Data3;

        public ServerToClient(long step, NativeList<Client> connections) : this()
        {
            Step = step;
            if (connections.Length > 0) Data0 = connections[0].InputBuffer.Input;
            if (connections.Length > 1) Data0 = connections[1].InputBuffer.Input;
            if (connections.Length > 2) Data0 = connections[2].InputBuffer.Input;
            if (connections.Length > 3) Data0 = connections[3].InputBuffer.Input;
        }

        public ServerToClient(DataStreamReader stream)
        {
            Step = stream.ReadLong();
            Data0 = stream.ReadByte();
            Data1 = stream.ReadByte();
            Data2 = stream.ReadByte();
            Data3 = stream.ReadByte();
        }

        public bool HasData => Step != 0;

        public void Apply(World world)
        {
            var entityManager = world.EntityManager;

            {
                var query = entityManager.CreateEntityQuery(new ComponentType(typeof(StepController)));
                var stepController = query.GetSingleton<StepController>();
                
                if (stepController.Step != Step - 1)
                {
                    Debug.LogError($"Failed step, tried to go from step {stepController.Step} to {Step}");
                    return;
                }
                    
                entityManager.SetComponentData(query.GetSingletonEntity(), new StepController(Step));
            }
            {
                var query = entityManager.CreateEntityQuery(new ComponentType(typeof(StepInput)), new ComponentType(typeof(PlayerControlledTag)));
                var entities = query.ToEntityArray(Allocator.Temp);
                if (entities.Length > 0)
                    entityManager.SetComponentData(entities[0], new StepInput(Data0));
                entities.Dispose();
            }
            world.Update();
        }
    }
    
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
    public class PingServerBehaviour : MonoBehaviour
    {
        public const byte CODE_SendStep = 0b0000_0000;
        public const byte CODE_SendSave = 0b0000_0001;
    
        /// <summary>UI component on which to set the join code.</summary>
        public PingUIBehaviour PingUI;

        private BiggerDriver m_ServerDriver;
        private NativeList<Client> m_ServerConnections;
        private NativeReference<ServerToClient> m_ServerToClient;
        
        private NativeArray<byte> m_SaveBuffer;

        private float m_CumulativeTime;
        
        // Handle to the job chain of the ping server. We need to keep this around so that we can
        // schedule the jobs in one execution of Update and complete it in the next.
        private JobHandle m_ServerJobHandle;
        private Game m_Game;

        private void Start()
        {
            m_ServerConnections = new NativeList<Client>(16, Allocator.Persistent);
            m_ServerToClient = new NativeReference<ServerToClient>(Allocator.Persistent);
            m_SaveBuffer = new NativeArray<byte>(PingClientBehaviour.k_MaxSaveSize, Allocator.Persistent);
            m_Game = new Game();
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

            var relayServerData = allocation.ToRelayServerData("wss");
            var settings = new NetworkSettings();
            settings.WithRelayParameters(serverData: ref relayServerData);
            settings.WithFragmentationStageParameters(payloadCapacity: 2_000_000);

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
            public NativeList<Client> Connections;

            public void Execute()
            {
                // Remove old connections from the list of active connections;
                for (int i = 0; i < Connections.Length; i++)
                {
                    if (!Connections[i].Connection.IsCreated)
                    {
                        Connections.RemoveAtSwapBack(i);
                        i--; // Need to re-process index i since it's a new connection.
                    }
                }

                // Accept all new connections.
                NetworkConnection connection;
                while ((connection = Driver.Driver.Accept()) != default)
                {
                    Debug.Log($"Got new client: {connection}");
                    Connections.Add(new Client(connection));
                }
            }
        }

        // Job to process events from all connections in parallel.
        [BurstCompile]
        private unsafe struct ConnectionsRelayEventsJobs : IJobParallelForDefer
        {
            public NetworkDriver.Concurrent Driver;
            public NativeArray<Client> Connections;
            
            [NativeDisableContainerSafetyRestriction]
            public NativeReference<ServerToClient> ServerToClient;

            public unsafe void Execute(int i)
            {
                var connection = Connections[i];

                NetworkEvent.Type eventType;
                while ((eventType = Driver.PopEventForConnection(connection.Connection, out var reader)) != NetworkEvent.Type.Empty)
                {
                    if (eventType == NetworkEvent.Type.Data)
                    {
                        switch (reader.ReadByte())
                        {
                            case PingClientBehaviour.CODE_SendInput:
                                connection.InputBuffer.Input = reader.ReadByte();
                                connection.HasInput = true;
                                break;
                            case PingClientBehaviour.CODE_RequestSave:
                                Debug.Log($"Save requested...");
                                connection.RequestedSave = true;
                                break;
                        }
                        Connections[i] = connection;
                    }
                    else if (eventType == NetworkEvent.Type.Disconnect)
                    {
                        // By making it default-valued, the connections update job will clean it up.
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
                    writer.WriteLong(ServerToClient.Value.Step);
                    writer.WriteByte(ServerToClient.Value.Data0);
                    writer.WriteByte(ServerToClient.Value.Data1);
                    writer.WriteByte(ServerToClient.Value.Data2);
                    writer.WriteByte(ServerToClient.Value.Data3);

                    result = Driver.EndSend(writer);
                    if (result < 0)
                    {
                        Debug.LogError($"Couldn't send ping answer (error code {result}).");
                        return;
                    }
                }
            }
        }
        
        [EditorButton]
        public void Reload()
        {
            m_ServerJobHandle.Complete();
            m_Game.ReloadSave();
        }

        private void Update()
        {
            if (m_ServerDriver.IsCreated)
            {
                // First, complete the previously-scheduled job chain.
                m_ServerJobHandle.Complete();
                
                for (int i = 0; i < m_ServerConnections.Length; i++)
                    if (m_ServerConnections[i].RequestedSave)
                    {
                        var con = m_ServerConnections[i];
                        SendSaveToConnection(m_ServerDriver, con);
                        con.RequestedSave = false;
                        m_ServerConnections[i] = con;
                    }

                // Create the jobs first.
                var updateJob = new ConnectionsRelayUpdateJob
                {
                    Driver = m_ServerDriver,
                    Connections = m_ServerConnections
                };
                var eventsJobs = new ConnectionsRelayEventsJobs
                {
                    Driver = m_ServerDriver.Driver.ToConcurrent(),
                    Connections = m_ServerConnections.AsDeferredJobArray(),
                };
                
                if (m_ServerConnections.Length > 0)
                {
                        
                    m_CumulativeTime += Time.deltaTime;
                    if (m_CumulativeTime >= PingClientBehaviour.k_PingFrequency)
                    {
                        var old = m_ServerToClient.Value;
                        m_ServerToClient.Value = new ServerToClient(old.Step + 1, m_ServerConnections);
                        eventsJobs.ServerToClient = m_ServerToClient;
                        Debug.Log($"Server: {m_ServerToClient.Value.Step}");
                        m_ServerToClient.Value.Apply(m_Game.World);
                    }
                }

                // Schedule the job chain.
                m_ServerJobHandle = m_ServerDriver.Driver.ScheduleUpdate();
                m_ServerJobHandle = updateJob.Schedule(m_ServerJobHandle);
                m_ServerJobHandle = eventsJobs.Schedule(m_ServerConnections, 1, m_ServerJobHandle);
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
    }
}
