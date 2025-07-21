using System;
using Unity.Burst;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport;

public class ServerTransport : MonoBehaviour
{
    const int CON_BUF_SIZE = 256;
    const int SEND_BUF_SIZE = 4;
    const int BUF_RING_LEN = 4;

    const int MAX_CONNNECTIONS = 32;

    MultiNetworkDriver m_Driver;
    NativeList<NetworkConnection> m_Connections;
    NativeList<byte> m_ConnectionReadBuffers;
    NativeArray<NativeArray<byte>> m_MessageRingBuffer;
    int _ringIndex;
    int _ringSteps;

    JobHandle m_ServerJobHandle;
    
    [EditorButton]
    public void TestHost(int port)
    {
        if (port == 0) port = 25565;
        Execute(port, 4);
    }

    private void OnEnable()
    {
        if (!m_Driver.IsCreated)
            enabled = false;
    }

    public void Execute(int port, int maxConnections)
    {
        if (m_Driver.IsCreated)
        {
            Debug.LogError($"{this} is already executing!");
            return;
        }

        if (maxConnections > MAX_CONNNECTIONS)
        {
            Debug.LogError($"Cannot run server with {maxConnections}/{MAX_CONNNECTIONS}");
            return;
        }

#if !UNITY_EDITOR && UNITY_WEBGL
            Debug.LogError("Server hosting not supported on webgl.");
            Destroy(this);
            return;
#endif

        Debug.LogError($"Server start, listening to port {port}.");
        var ipv4Driver = NetworkDriver.Create();
        ipv4Driver.Bind(NetworkEndpoint.AnyIpv4.WithPort((ushort)port));
        ipv4Driver.Listen();
        var ipv4Driver_WEB = NetworkDriver.Create(new WebSocketNetworkInterface());
        ipv4Driver_WEB.Bind(NetworkEndpoint.AnyIpv4.WithPort((ushort)port));
        ipv4Driver_WEB.Listen();
        var ipv6Driver = NetworkDriver.Create();
        ipv6Driver.Bind(NetworkEndpoint.AnyIpv6.WithPort((ushort)port));
        ipv6Driver.Listen();
        var ipv6Driver_WEB = NetworkDriver.Create(new WebSocketNetworkInterface());
        ipv6Driver_WEB.Bind(NetworkEndpoint.AnyIpv6.WithPort((ushort)port));
        ipv6Driver_WEB.Listen();
        
        m_Driver = MultiNetworkDriver.Create();
        m_Driver.AddDriver(ipv4Driver);
        m_Driver.AddDriver(ipv4Driver_WEB);
        m_Driver.AddDriver(ipv6Driver);
        m_Driver.AddDriver(ipv6Driver_WEB);
        
        m_Connections = new NativeList<NetworkConnection>(maxConnections, Allocator.Persistent);
        m_ConnectionReadBuffers = new NativeList<byte>(maxConnections*CON_BUF_SIZE, Allocator.Persistent);
        
        m_MessageRingBuffer = new NativeArray<NativeArray<byte>>(BUF_RING_LEN, Allocator.Persistent);
        for (int i = 0; i < m_MessageRingBuffer.Length; i++)
            m_MessageRingBuffer[i] = new NativeArray<byte>(SEND_BUF_SIZE, Allocator.Persistent);

        enabled = true;
    }
    
    void Cleanup()
    {
        if (m_Driver.IsCreated)
        {
            m_ServerJobHandle.Complete();
            m_Driver.Dispose();
            m_Connections.Dispose();
            m_ConnectionReadBuffers.Dispose();

            for (int i = 0; i < m_MessageRingBuffer.Length; i++)
                m_MessageRingBuffer[i].Dispose();
            m_MessageRingBuffer.Dispose();
        }
    }

    void OnDestroy()
    {
        Cleanup();
    }

    void Update()
    {
        m_ServerJobHandle.Complete();

        var connectionJob = new ServerUpdateConnectionsJob
        {
            Driver = m_Driver,
            Connections = m_Connections,
            ConnectionReadBuffers = m_ConnectionReadBuffers
        };

        var serverUpdateJob = new ServerUpdateJob
        {
            Driver = m_Driver.ToConcurrent(),
            Connections = m_Connections.AsDeferredJobArray(),
            ReadBuffers = m_ConnectionReadBuffers.AsDeferredJobArray(),
        };
        
        if (_ringSteps > 0)
        {
            
            _ringSteps--;
            _ringIndex = (_ringIndex + 1) % m_MessageRingBuffer.Length;
            serverUpdateJob.MessageAll = m_MessageRingBuffer[_ringIndex];
            Debug.Log($"Sending message at {_ringIndex}");
        }

        m_ServerJobHandle = m_Driver.ScheduleUpdate();
        m_ServerJobHandle = connectionJob.Schedule(m_ServerJobHandle);
        m_ServerJobHandle = serverUpdateJob.Schedule(m_Connections, 1, m_ServerJobHandle);

    }

    [EditorButton]
    public void SendAllTest()
    {
        SendAll(new byte[4]
        {
            0, 1, 2, 3
        });
    }

    public void SendAll(byte[] bytes)
    {
        if (_ringSteps < m_MessageRingBuffer.Length - 1)
            _ringSteps++;
        else if (m_MessageRingBuffer.Length == 0)
        {
            Debug.LogError($"Server not running, cannot send.");
            return;
        }
        else
            Debug.LogError($"Too many messages queued, old data lost.");

        var index = (_ringIndex + _ringSteps) % m_MessageRingBuffer.Length;
        m_MessageRingBuffer[index].CopyFromAndZero(bytes);
    }

    public void SendAll(NativeArray<byte> bytes)
    {
        if (_ringSteps < m_MessageRingBuffer.Length - 1)
            _ringSteps++;
        else if (m_MessageRingBuffer.Length == 0)
        {
            Debug.LogError($"Server not running, cannot send.");
            return;
        }
        else
            Debug.LogError($"Too many messages queued, old data lost.");
            
        if (_ringSteps < m_MessageRingBuffer.Length - 1)
            _ringSteps++;
        else
            Debug.LogError($"Too many messages queued, old data lost.");

        var index = (_ringIndex + _ringSteps) % m_MessageRingBuffer.Length;
        m_MessageRingBuffer[index].CopyFromAndZero(bytes);
    }

    [BurstCompile]
    struct ServerUpdateConnectionsJob : IJob
    {
        public MultiNetworkDriver Driver;
        public NativeList<NetworkConnection> Connections;
        public NativeList<byte> ConnectionReadBuffers;

        public void Execute()
        {
            // Clean up connections.
            for (int i = 0; i < Connections.Length; i++)
            {
                if (!Connections[i].IsCreated)
                {
                    Connections.RemoveAtSwapBack(i);
                    ConnectionReadBuffers.RemoveRangeSwapBack(CON_BUF_SIZE*i, CON_BUF_SIZE);
                    i--;
                }
            }

            // Accept new connections.
            NetworkConnection c;
            while ((c = Driver.Accept()) != default)
            {
                Connections.Add(c);
                ConnectionReadBuffers.AddReplicate(0, CON_BUF_SIZE);
                Debug.Log("Accepted a connection.");
            }
        }
    }

    [BurstCompile]
    unsafe struct ServerUpdateJob : IJobParallelForDefer
    {
        public MultiNetworkDriver.Concurrent Driver;
        public NativeArray<NetworkConnection> Connections;
        public NativeArray<byte> ReadBuffers;
        
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<byte> MessageAll;

        public void Execute(int i)
        {
            DataStreamReader stream;
            NetworkEvent.Type cmd;
            while ((cmd = Driver.PopEventForConnection(Connections[i], out stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    var len = stream.Length;
                    stream.ReadBytes(new Span<byte>(&((byte*)ReadBuffers.GetUnsafeReadOnlyPtr())[i*CON_BUF_SIZE], len));
                    Debug.Log($"Got {len} bytes from client {Connections[i]}...");

                    Debug.Log($"... sending '{len}' to client {Connections[i]}.");
                    Driver.BeginSend(Connections[i], out var writer);
                    writer.WriteInt(len);
                    Driver.EndSend(writer);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client disconnected from the server.");
                    Connections[i] = default;
                }
            }
            
            if (Connections[i].IsCreated && MessageAll.IsCreated)
            {
                Debug.Log($"Sending {MessageAll.Length} bytes to client {Connections[i]}...");
                Driver.BeginSend(Connections[i], out var writer);
                writer.WriteBytes(MessageAll);
                Driver.EndSend(writer);
            }
        }
    }
}