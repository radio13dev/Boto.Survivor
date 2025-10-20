using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

[Serializable]
[StructLayout(LayoutKind.Explicit, Size = StepInput.Length*PingServerBehaviour.k_MaxPlayerCount + 12, Pack = 4)]
public unsafe struct FullStepData
{
    
    public const int Length = PingServerBehaviour.k_MaxPlayerCount;
    [SerializeField] [FieldOffset(0)] public long Step;
    [SerializeField] [FieldOffset(8)] public byte ExtraActionCount;
    [SerializeField] [FieldOffset(12)] public fixed long P[PingServerBehaviour.k_MaxPlayerCount];

    public bool HasData => Step != 0;

    public StepInput this[int index]
    {
        get
        {
            fixed (long* p = P)
                return *(((StepInput*)p) + index);
        }

        set
        {
            fixed (long* p = P)
                ((StepInput*)p)[index] = value;
        }
    }

    public FullStepData(long step, NativeArray<Client> connections) : this()
    {
        Step = step;
        for (int i = 0; i < connections.Length; i++)
            this[i] = connections[i].InputBuffer;
    }
    public FullStepData(long step, params StepInput[] inputs) : this()
    {
        Step = step;
        for (int i = 0; i < inputs.Length; i++)
            this[i] = inputs[i];
    }

    public unsafe void Write(ref DataStreamWriter writer, byte extraActionCount, GameRpc* extraActionPtr)
    {
        writer.WriteLong(Step);
        
        for (int i = 0; i < PingServerBehaviour.k_MaxPlayerCount; i++)
            this[i].Write(ref writer);
        
        writer.WriteByte(extraActionCount);
        if (extraActionCount > 0)
            for (byte i = 0; i < extraActionCount; i++)
                extraActionPtr[i].Write(ref writer);
    }

    public static unsafe FullStepData Read(ref DataStreamReader reader, GameRpc* extraActionPtr)
    {
        FullStepData result = new();
        result.Step = reader.ReadLong();
        for (int i = 0; i < PingServerBehaviour.k_MaxPlayerCount; i++)
            result[i] = StepInput.Read(ref reader);
        
        result.ExtraActionCount = reader.ReadByte();
        if (result.ExtraActionCount > 0)
            for (byte i = 0; i < result.ExtraActionCount; i++)
                extraActionPtr[i] = GameRpc.Read(ref reader);
                
        return result;
    }
}