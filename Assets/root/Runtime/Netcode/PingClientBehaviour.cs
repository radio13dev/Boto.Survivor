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
using UnityEngine.InputSystem;

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

    public const int k_MaxSaveSize = 200_000;

    /// <summary>UI component to get the join code and update statistics.</summay>
    public PingUIBehaviour PingUI;

    public Game Game => m_Game;

    // Frequency (in seconds) at which to send ping messages.
    public const float k_PingFrequency = 1.0f / 60.0f;
    public const int k_FrameDelay = 1;

    private BiggerDriver m_ClientDriver;
    private NativeReference<StepInput> m_FrameInput;
    private NativeQueue<FullStepData> m_ServerMessageBuffer;
    private NativeArray<SpecialLockstepActions> m_SpecialActionArr;

    private NativeArray<byte> m_SaveBuffer;

    // Connection and ping statistics. Values that can be modified by a job are stored in
    // NativeReferences since jobs can only modify values in native containers.
    private NativeReference<Server> m_ClientConnection;
    private float m_CumulativeTime;

    // Handle to the job chain of the ping client. We need to keep this around so that we can
    // schedule the jobs in one execution of Update and complete it in the next.
    private JobHandle m_ClientJobHandle;
    private Game m_Game;

    private void Start()
    {
        m_ClientConnection = new NativeReference<Server>(Allocator.Persistent);
        m_FrameInput = new NativeReference<StepInput>(Allocator.Persistent);
        m_ServerMessageBuffer = new NativeQueue<FullStepData>(Allocator.Persistent);
        m_SaveBuffer = new NativeArray<byte>(k_MaxSaveSize, Allocator.Persistent);
        m_SpecialActionArr = new NativeArray<SpecialLockstepActions>(4, Allocator.Persistent);
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
        var settings = new NetworkSettings();
        settings.WithRelayParameters(serverData: ref relayServerData);
        settings.WithFragmentationStageParameters(payloadCapacity: 2_000_000);

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
            writer.WriteByte(FrameInput.Value.Input);
            FrameInput.Value = default;

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
        public NativeArray<byte> SaveBuffer;
        public NativeArray<SpecialLockstepActions> SpecialActionsArray;

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
                                ServerMessageBuffer.Enqueue(FullStepData.Read(ref reader, (SpecialLockstepActions*)SpecialActionsArray.GetUnsafePtr()));
                                break;
                            case PingServerBehaviour.CODE_SendSave:

                                Debug.Log($"... got save...");

                                var len = reader.ReadInt();
                                Debug.Log($"... len {len}...");

                                ((int*)SaveBuffer.GetUnsafePtr())[0] = len; // Write the length to the first 4 bytes of the save buffer
                                reader.ReadBytes(new Span<byte>(&(((byte*)SaveBuffer.GetUnsafePtr())[4]), len)); // ... then write the rest of the save out
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

            var memLen = ((int*)m_SaveBuffer.GetUnsafePtr())[0];
            if (memLen != 0)
            {
                Debug.Log($"... loading save...");

                if (m_Game == null)
                    m_Game = new Game();

                // Load this in
                ((int*)m_SaveBuffer.GetUnsafePtr())[0] = 0;
                m_Game.LoadSave(&((byte*)m_SaveBuffer.GetUnsafePtr())[4], memLen);

                Debug.Log($"... done.");
            }

            m_CumulativeTime += Time.deltaTime;
            bool shouldSend = m_CumulativeTime >= k_PingFrequency;
            if (shouldSend) m_CumulativeTime -= k_PingFrequency;

            if (m_Game != null && m_Game.IsReady && (shouldSend || m_ServerMessageBuffer.Count > k_FrameDelay) && m_ServerMessageBuffer.TryDequeue(out var msg))
            {
                msg.Apply(m_Game.World, (SpecialLockstepActions*)m_SpecialActionArr.GetUnsafePtr());
            }

            if (m_FrameInput.IsCreated)
            {
                var val = m_FrameInput.Value;

                if (Keyboard.current.wKey.isPressed) val.Input |= StepInput.UpInput;
                if (Keyboard.current.sKey.isPressed) val.Input |= StepInput.DownInput;
                if (Keyboard.current.aKey.isPressed) val.Input |= StepInput.LeftInput;
                if (Keyboard.current.dKey.isPressed) val.Input |= StepInput.RightInput;

                if (Keyboard.current.eKey.isPressed) val.Input |= StepInput.S1Input;
                if (Keyboard.current.qKey.isPressed) val.Input |= StepInput.S2Input;
                if (Keyboard.current.spaceKey.isPressed) val.Input |= StepInput.S3Input;
                if (Keyboard.current.shiftKey.isPressed) val.Input |= StepInput.S4Input;

                m_FrameInput.Value = val;
            }

            var updateJob = new PingRelayUpdateJob
            {
                Driver = m_ClientDriver,
                Connection = m_ClientConnection,
                ServerMessageBuffer = m_ServerMessageBuffer,
                SaveBuffer = m_SaveBuffer,
                SpecialActionsArray = m_SpecialActionArr
            };

            // If it's time to send, schedule a send job.
            var state = m_ClientDriver.Driver.GetConnectionState(m_ClientConnection.Value.Connection);
            if (state == NetworkConnection.State.Connected && shouldSend)
            {
                m_ClientJobHandle = new PingRelaySendJob
                {
                    Driver = m_ClientDriver.Driver.ToConcurrent(),
                    Connection = m_ClientConnection.Value.Connection,
                    FrameInput = m_FrameInput
                }.Schedule();
            }

            // Schedule a job chain with the ping send job (if scheduled), the driver update
            // job, and then the ping update job. All jobs will run one after the other.
            m_ClientJobHandle = m_ClientDriver.Driver.ScheduleUpdate(m_ClientJobHandle);
            m_ClientJobHandle = updateJob.Schedule(m_ClientJobHandle);
        }
    }
}