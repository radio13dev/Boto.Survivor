using System;
using System.Collections;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public static class Netcode
{
    public const int k_MaxFragmentSize = 20_000_000;

    public static NetworkSettings NetworkSettings(ref RelayServerData relayServerData)
    {
        var settings = new NetworkSettings();
        settings.WithRelayParameters(serverData: ref relayServerData);
        settings.WithFragmentationStageParameters(payloadCapacity: k_MaxFragmentSize);
        return settings;
    }
}

public unsafe struct Server
{
    public NetworkConnection Connection;

    public Server(NetworkConnection connection)
    {
        Connection = connection;
    }
}

public unsafe struct BiggerDriver : IDisposable
{
    public NetworkDriver Driver;
    public NetworkPipeline LargePipeline;

    public BiggerDriver(NetworkDriver driver)
    {
        Driver = driver;
        LargePipeline = Driver.CreatePipeline(typeof(FragmentationPipelineStage));
    }

    public bool IsCreated => Driver.IsCreated;

    public void Dispose()
    {
        Driver.Dispose();
    }
}

/// <summary>Component responsible for sending pings to the server.</summary>
public unsafe class PingClientBehaviour : MonoBehaviour
{
    public const byte CODE_SendInput = 0b0000_0000;
    public const byte CODE_RequestSave = 0b0000_0001;
    public const byte CODE_SendRpc = 0b0000_0010;

    public const int k_MaxSaveSize = 200_000;
    public const int k_MaxSpecialActionCount = 4;

    /// <summary>UI component to get the join code and update statistics.</summay>
    public PingUIBehaviour PingUI;

    public Game Game => m_Game;

    // Frequency (in seconds) at which to send ping messages.
    public const int k_FrameDelay = 3;

    private BiggerDriver m_ClientDriver;
    private NativeReference<StepInput> m_FrameInput;
    private NativeQueue<FullStepData> m_ServerMessageBuffer;
    private NativeQueue<GenericMessage> m_GenericMessageBuffer;
    private NativeArray<SpecialLockstepActions> m_SpecialActionArr;

    private NativeList<byte> m_SaveBuffer;

    // Connection and ping statistics. Values that can be modified by a job are stored in
    // NativeReferences since jobs can only modify values in native containers.
    private NativeReference<Server> m_ClientConnection;
    private float m_CumulativeTime;

    // Handle to the job chain of the ping client. We need to keep this around so that we can
    // schedule the jobs in one execution of Update and complete it in the next.
    private JobHandle m_ClientJobHandle;
    private Game m_Game;

    private IEnumerator Start()
    {
        if (Game.ClientGame == null ||
            (Game.ClientGame != null && Game.ClientGame.IsReady))
        {
            Debug.Log($"Running multiple singleplayer games at the same time, creating new one...");
            Game.ClientGame = new Game(true);
            World.DefaultGameObjectInjectionWorld = Game.ClientGame.World;
        }
        
        // Build game
        yield return new WaitUntil(() => Game.ConstructorReady && Game.ClientGame != null);
        m_Game = Game.ClientGame;
        
        m_ClientConnection = new NativeReference<Server>(Allocator.Persistent);
        m_FrameInput = new NativeReference<StepInput>(Allocator.Persistent);
        m_ServerMessageBuffer = new NativeQueue<FullStepData>(Allocator.Persistent);
        m_GenericMessageBuffer = new NativeQueue<GenericMessage>(Allocator.Persistent);
        m_SaveBuffer = new NativeList<byte>(Allocator.Persistent);
        m_SpecialActionArr = new NativeArray<SpecialLockstepActions>(k_MaxSpecialActionCount, Allocator.Persistent);
    }

    private void OnDestroy()
    {
        if (m_ClientDriver.IsCreated)
        {
            // All jobs must be completed before we can dispose of the data they use.
            m_ClientJobHandle.Complete();
            m_ClientDriver.Dispose();
        }

        m_ClientConnection.Dispose();
        m_FrameInput.Dispose();
        m_ServerMessageBuffer.Dispose();
        m_GenericMessageBuffer.Dispose();
        m_SaveBuffer.Dispose();
        m_SpecialActionArr.Dispose();
    }

    /// <summary>Start establishing a connection to the server.</summary>
    /// <returns>Enumerator for a coroutine.</returns>
    public IEnumerator Connect()
    {
        var joinTask = RelayService.Instance.JoinAllocationAsync(PingUI.JoinCode);
        while (!joinTask.IsCompleted)
            yield return null;

        if (joinTask.IsFaulted)
        {
            Debug.LogError("Failed to join the Relay allocation.");
            yield break;
        }


        var relayServerData = joinTask.Result.ToRelayServerData("wss");
        var settings = Netcode.NetworkSettings(ref relayServerData);
        m_ClientDriver = new BiggerDriver(NetworkDriver.Create(new WebSocketNetworkInterface(), settings));

        m_ClientConnection.Value = new Server(m_ClientDriver.Driver.Connect());
    }

    // Job that will send ping messages to the server.
    [BurstCompile]
    private struct PingRelaySendJob : IJob
    {
        public NetworkDriver.Concurrent Driver;
        public NetworkConnection Connection;
        public NativeReference<StepInput> FrameInput;

        public void Execute()
        {
            var result = Driver.BeginSend(Connection, out var writer);
            if (result < 0)
            {
                Debug.LogError($"Couldn't send ping (error code {result}).");
                return;
            }

            writer.WriteByte(CODE_SendInput);
            FrameInput.Value.Write(ref writer);
            FrameInput.Value = default;

            result = Driver.EndSend(writer);
            if (result < 0)
            {
                Debug.LogError($"Couldn't send ping (error code {result}).");
                return;
            }
        }
    }
    
    // Job that sends an Rpc to the server.
    [BurstCompile]
    private struct PingRelayRpcJob : IJob
    {
        public NetworkDriver.Concurrent Driver;
        public NetworkConnection Connection;
        public SpecialLockstepActions Rpc;

        public void Execute()
        {
            var result = Driver.BeginSend(Connection, out var writer);
            if (result < 0)
            {
                Debug.LogError($"Couldn't send ping (error code {result}).");
                return;
            }

            writer.WriteByte(CODE_SendRpc);
            Rpc.Write(ref writer);

            result = Driver.EndSend(writer);
            if (result < 0)
            {
                Debug.LogError($"Couldn't send ping (error code {result}).");
                return;
            }
        }
    }

    // Job to run after a driver update, which will deal with update the client connection and
    // reacting to ping messages received by the servers.
    [BurstCompile]
    private unsafe struct PingRelayUpdateJob : IJob
    {
        public BiggerDriver Driver;
        public NativeReference<Server> Connection;
        public NativeQueue<FullStepData> ServerMessageBuffer;
        public NativeQueue<GenericMessage>.ParallelWriter GenericMessageBuffer;
        public NativeList<byte> SaveBuffer;
        public NativeArray<SpecialLockstepActions> SpecialActionsArray;
        public long Now;

        public void Execute()
        {
            NetworkEvent.Type eventType;
            while ((eventType = Connection.Value.Connection.PopEvent(Driver.Driver, out var reader, out var pipeline)) != NetworkEvent.Type.Empty)
            {
                switch (eventType)
                {
                    // Connect event means the connection has been established.
                    case NetworkEvent.Type.Connect:
                        Debug.Log("Connected to server.");
                        break;

                    // Disconnect event means the connection was closed (server exited).
                    case NetworkEvent.Type.Disconnect:
                        Debug.Log("Got disconnected from server.");

                        // Storing a default value as the connection will make the Update method
                        // retry to connect when it next executes.
                        Connection.Value = default;
                        break;

                    // Data events must be ping messages sent by the server.
                    case NetworkEvent.Type.Data:
                        switch (reader.ReadByte())
                        {
                            case PingServerBehaviour.CODE_SendStep:
                                var stepData = FullStepData.Read(ref reader, (SpecialLockstepActions*)SpecialActionsArray.GetUnsafePtr());
                                ServerMessageBuffer.Enqueue(stepData);
                                //NetworkPing.ClientPingTimes.Data.Add((Now, (int)stepData.Step));
                                break;
                            case PingServerBehaviour.CODE_SendSave:

                                Debug.Log($"... got save...");

                                var len = reader.ReadInt();
                                Debug.Log($"... len {len}...");
                                SaveBuffer.Resize(len, NativeArrayOptions.UninitializedMemory);
                                reader.ReadBytes(SaveBuffer.AsArray());
                                break;
                            case PingServerBehaviour.CODE_SendId:
                                GenericMessageBuffer.Enqueue(GenericMessage.Read(ref reader));
                                break;
                            default:
                                Debug.Log($"... got mystery message from server.");
                                break;
                        }

                        break;
                }
            }
        }
    }

    [EditorButton]
    private void RequestNewSave()
    {
        if (m_ClientDriver.IsCreated)
        {
            m_ClientJobHandle.Complete();

            var result = m_ClientDriver.Driver.BeginSend(m_ClientConnection.Value.Connection, out var writer);
            if (result < 0)
            {
                Debug.LogError($"Couldn't send ping (error code {result}).");
                return;
            }

            Debug.Log($"Requesting save...");
            writer.WriteByte(CODE_RequestSave);

            result = m_ClientDriver.Driver.EndSend(writer);
            if (result < 0)
            {
                Debug.LogError($"Couldn't send ping (error code {result}).");
                return;
            }
        }
    }

    private void Update()
    {
        if (m_ClientDriver.IsCreated)
        {
            // First, complete the previously-scheduled job chain.
            m_ClientJobHandle.Complete();

            if (m_SaveBuffer.Length != 0)
            {
                Debug.Log($"... loading save {m_SaveBuffer.Length}...");

                // Load this in
                m_Game.LoadSave(m_SaveBuffer.AsArray());
                m_SaveBuffer.Clear();
                m_Game.LoadScenes();

                Debug.Log($"... done.");
            }

            m_CumulativeTime += Time.deltaTime;
            bool shouldSend = m_CumulativeTime >= Game.k_ClientPingFrequency;
            if (shouldSend) m_CumulativeTime = 0;

            if (m_Game != null)
            {
                bool ready = m_Game.IsReady;
                if (ready && m_ServerMessageBuffer.Count > 0 && (shouldSend || m_ServerMessageBuffer.Count > k_FrameDelay) && ClientDesyncDebugger.CanExecuteStep(m_ServerMessageBuffer.Peek().Step) && m_ServerMessageBuffer.TryDequeue(out var msg))
                {
                    m_Game.ApplyStepData(msg, (SpecialLockstepActions*)m_SpecialActionArr.GetUnsafePtr());
                    NetworkPing.ClientExecuteTimes.Data.Add((DateTime.Now, (int)msg.Step));
                }
                else if (!ready) m_Game.World.Update(); // Completes save loading
                else
                {
                    // Render between steps
                    m_Game.ApplyRender(math.clamp(m_CumulativeTime / Game.k_ClientPingFrequency, 0, 1));
                }
                
                while (m_GenericMessageBuffer.TryDequeue(out var message))
                {
                    message.Execute(m_Game);
                }
            }


            if (m_FrameInput.IsCreated)
            {
                var val = m_FrameInput.Value;
                val.Collect(Camera.main);
                m_FrameInput.Value = val;
            }

            var updateJob = new PingRelayUpdateJob
            {
                Driver = m_ClientDriver,
                Connection = m_ClientConnection,
                ServerMessageBuffer = m_ServerMessageBuffer,
                GenericMessageBuffer = m_GenericMessageBuffer.AsParallelWriter(),
                SaveBuffer = m_SaveBuffer,
                SpecialActionsArray = m_SpecialActionArr,
                Now = DateTime.Now.Ticks
            };

            // If it's time to send, schedule a send job.
            var state = m_ClientDriver.Driver.GetConnectionState(m_ClientConnection.Value.Connection);
            if (state == NetworkConnection.State.Connected)
            {
                // Send off any queued rpcs
                NetworkDriver.Concurrent concurrentDriver = m_ClientDriver.Driver.ToConcurrent();
                while (m_Game != null && m_Game.RpcSendBuffer.TryDequeue(out var rpc))
                {
                    m_ClientJobHandle = new PingRelayRpcJob()
                    {
                        Driver = concurrentDriver,
                        Connection = m_ClientConnection.Value.Connection,
                        Rpc = rpc
                    }.Schedule(m_ClientJobHandle);
                }
            
                if (shouldSend)
                {
                    m_ClientJobHandle = new PingRelaySendJob
                    {
                        Driver = concurrentDriver,
                        Connection = m_ClientConnection.Value.Connection,
                        FrameInput = m_FrameInput
                    }.Schedule(m_ClientJobHandle);
                }
            }

            // Schedule a job chain with the ping send job (if scheduled), the driver update
            // job, and then the ping update job. All jobs will run one after the other.
            m_ClientJobHandle = m_ClientDriver.Driver.ScheduleUpdate(m_ClientJobHandle);
            m_ClientJobHandle = updateJob.Schedule(m_ClientJobHandle);
        }
    }
}