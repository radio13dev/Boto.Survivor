using System;
using Unity.Burst;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Networking.Transport;

public class ClientTransport : MonoBehaviour
{
    const int READ_BUFFER_SIZE = 256;
    const int SEND_BUF_SIZE = 4;
    const int BUF_RING_LEN = 4;

    NetworkDriver m_Driver;
    NativeArray<NetworkConnection> m_Connection;
    NativeArray<byte> m_ReadBuffer;
    NativeArray<NativeArray<byte>> m_MessageRingBuffer;
    int _ringIndex;
    int _ringSteps;

    JobHandle m_ClientJobHandle;

    [EditorButton]
    public void TestConnect(int port)
    {
        if (port == 0) port = 7978;
        Execute(NetworkEndpoint.LoopbackIpv4.WithPort((ushort)port));
    }

    private void OnEnable()
    {
        if (!m_Driver.IsCreated)
            enabled = false;
    }

    public void Execute(NetworkEndpoint connectEndpoint)
    {
        if (m_Driver.IsCreated)
        {
            Debug.LogError($"{this} is already executing!");
            return;
        }

        Debug.Log($"{this} connecting to {connectEndpoint}...");
        m_Driver = NetworkDriver.Create();
        m_Connection = new NativeArray<NetworkConnection>(1, Allocator.Persistent);
        m_Connection[0] = m_Driver.Connect(connectEndpoint);
        m_ReadBuffer = new NativeArray<byte>(READ_BUFFER_SIZE, Allocator.Persistent);
        
        m_MessageRingBuffer = new NativeArray<NativeArray<byte>>(BUF_RING_LEN, Allocator.Persistent);
        for (int i = 0; i < m_MessageRingBuffer.Length; i++)
            m_MessageRingBuffer[i] = new NativeArray<byte>(SEND_BUF_SIZE, Allocator.Persistent);
        
        enabled = true;
    }

    void Cleanup()
    {
        m_ClientJobHandle.Complete();
        
        if (m_Driver.IsCreated && m_Connection.Length > 0 && m_Connection[0].IsCreated)
        {
            m_Connection[0].Disconnect(m_Driver);
            Update();
            m_ClientJobHandle.Complete();
        }

        m_Driver.Dispose();
        m_Connection.Dispose();
        m_ReadBuffer.Dispose();
        
        for (int i = 0; i < m_MessageRingBuffer.Length; i++)
            m_MessageRingBuffer[i].Dispose();
        m_MessageRingBuffer.Dispose();
    }

    void OnDestroy()
    {
        Cleanup();
    }

    void Update()
    {
        m_ClientJobHandle.Complete();

        var job = new ClientUpdateJob
        {
            Driver = m_Driver,
            Connection = m_Connection,
            ReadBuffer = m_ReadBuffer
        };
        
        if (_ringSteps > 0)
        {
            
            _ringSteps--;
            _ringIndex = (_ringIndex + 1) % m_MessageRingBuffer.Length;
            job.WriteBuffer = m_MessageRingBuffer[_ringIndex];
            Debug.Log($"Sending message at {_ringIndex}");
        }
        
        m_ClientJobHandle = m_Driver.ScheduleUpdate();
        m_ClientJobHandle = job.Schedule(m_ClientJobHandle);
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
    unsafe struct ClientUpdateJob : IJob
    {
        public NetworkDriver Driver;
        public NativeArray<NetworkConnection> Connection;
        public NativeArray<byte> ReadBuffer;
        
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<byte> WriteBuffer;

        public void Execute()
        {
            if (!Connection[0].IsCreated)
            {
                return;
            }

            DataStreamReader stream;
            NetworkEvent.Type cmd;
            while ((cmd = Connection[0].PopEvent(Driver, out stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Connect)
                {
                    Debug.Log("We are now connected to the server.");
                }
                else if (cmd == NetworkEvent.Type.Data)
                {
                    var len = stream.Length;
                    stream.ReadBytes(new Span<byte>((byte*)ReadBuffer.GetUnsafeReadOnlyPtr(), len));
                    Debug.Log($"Got {len} bytes from server {Connection[0]}...");
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client got disconnected from the server.");
                    Connection[0] = default;
                }
            }
            
            if (WriteBuffer.IsCreated && Connection[0].IsCreated)
            {
                Driver.BeginSend(Connection[0], out var writer);
                writer.WriteBytes(WriteBuffer);
                Driver.EndSend(writer);
            }
        }
    }
}