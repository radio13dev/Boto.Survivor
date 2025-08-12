using System.Runtime.InteropServices;
using Unity.Collections;

[StructLayout(layoutKind: LayoutKind.Sequential)]
public struct FullStepData
{
    public long Step;
    public StepInput P0;
    public StepInput P1;
    public StepInput P2;
    public StepInput P3;
    public byte ExtraActionCount;

    public StepInput this[int index]
    {
        get
        {
            if (index == 0) return P0;
            if (index == 1) return P1;
            if (index == 2) return P2;
            if (index == 3) return P3;
            return default;
        }

        set
        {
            if (index == 0) P0 = value;
            if (index == 1) P1 = value;
            if (index == 2) P2 = value;
            if (index == 3) P3 = value;
        }
    }
    
    public int Length => PingServerBehaviour.k_MaxPlayerCount;

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

    public bool HasData => Step != 0;

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