using System.Runtime.InteropServices;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

[StructLayout(layoutKind: LayoutKind.Sequential)]
[Save]
public struct StepInput : IComponentData
{
    public byte Input;
    public float3 Direction;
        
    // @formatter:off
        public const byte AdjustInventoryInput = 0b0000_0100;
        
        public const byte S1Input       = 0b0001_0000;
        public const byte S2Input       = 0b0010_0000;
        public const byte S3Input       = 0b0100_0000;
        public const byte S4Input       = 0b1000_0000;
    // @formatter:on 

    public bool S1 => (Input & S1Input) > 0;

    public void Write(ref DataStreamWriter writer)
    {
        writer.WriteByte(Input);
        writer.WriteFloat(Direction.x);
        writer.WriteFloat(Direction.y);
        writer.WriteFloat(Direction.z);
    }

    public static StepInput Read(ref DataStreamReader reader)
    {
        var input = reader.ReadByte();
        var direction = new float3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
    
        return new StepInput(){Input = input, Direction = direction};
    }

    public void Collect(Camera camera)
    {
        if (Keyboard.current.wKey.isPressed)        Direction += (float3)camera.transform.up;
        if (Keyboard.current.sKey.isPressed)        Direction -= (float3)camera.transform.up;
        if (Keyboard.current.aKey.isPressed)        Direction -= (float3)camera.transform.right;
        if (Keyboard.current.dKey.isPressed)        Direction += (float3)camera.transform.right;
        
        if (Keyboard.current.jKey.isPressed)        Input |= StepInput.S1Input;
        if (Keyboard.current.kKey.isPressed)        Input |= StepInput.S2Input;
        if (Keyboard.current.spaceKey.isPressed)    Input |= StepInput.S3Input;
        if (Keyboard.current.shiftKey.isPressed)    Input |= StepInput.S4Input;
    }
}

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