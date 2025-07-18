using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

[StructLayout(layoutKind: LayoutKind.Sequential)]
public struct StepInput : IComponentData
{
    public byte Input;
    public float2 Direction;
        
    // @formatter:off
        public const byte S1Input       = 0b0001_0000;
        public const byte S2Input       = 0b0010_0000;
        public const byte S3Input       = 0b0100_0000;
        public const byte S4Input       = 0b1000_0000;
    // @formatter:on 

    public bool S1 => (Input & S1Input) > 0;

    public void Write(ref DataStreamWriter writer)
    {
        writer.WriteByte(Input);
    }

    public static StepInput Read(ref DataStreamReader reader)
    {
        var input = reader.ReadByte();
        var direction = new float2(reader.ReadFloat(), reader.ReadFloat());
    
        return new StepInput(){Input = input, Direction = direction};
    }

    public void Collect()
    {
        if (Keyboard.current.wKey.isPressed)        Direction += new float2(0,1);
        if (Keyboard.current.sKey.isPressed)        Direction += new float2(0,-1);
        if (Keyboard.current.aKey.isPressed)        Direction += new float2(-1,0);
        if (Keyboard.current.dKey.isPressed)        Direction += new float2(1,0);
        
        if (Keyboard.current.eKey.isPressed)        Input |= StepInput.S1Input;
        if (Keyboard.current.qKey.isPressed)        Input |= StepInput.S2Input;
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

    public unsafe void Write(ref DataStreamWriter writer, byte extraActionCount, SpecialLockstepActions* extraActionPtr)
    {
        writer.WriteLong(Step);
        P0.Write(ref writer);
        P1.Write(ref writer);
        P2.Write(ref writer);
        P3.Write(ref writer);
        
        writer.WriteByte(extraActionCount);
        if (extraActionCount > 0)
            for (byte i = 0; i < extraActionCount; i++)
                extraActionPtr[i].Write(ref writer);
    }

    public static unsafe FullStepData Read(ref DataStreamReader reader, SpecialLockstepActions* extraActionPtr)
    {
        FullStepData result = new();
        result.Step = reader.ReadLong();
        result.P0 = StepInput.Read(ref reader);
        result.P1 = StepInput.Read(ref reader);
        result.P2 = StepInput.Read(ref reader);
        result.P3 = StepInput.Read(ref reader);
        
        result.ExtraActionCount = reader.ReadByte();
        if (result.ExtraActionCount > 0)
            for (byte i = 0; i < result.ExtraActionCount; i++)
                extraActionPtr[i] = SpecialLockstepActions.Read(ref reader);
                
        return result;
    }

    public unsafe void Apply(World world, SpecialLockstepActions* extraActionPtr)
    {
        var entityManager = world.EntityManager;

        // Iterate step count
        {
            var query = entityManager.CreateEntityQuery(new ComponentType(typeof(StepController)));
            var stepController = query.GetSingleton<StepController>();
            
            if (Step == -1)
                Step = stepController.Step + 1;

            if (Step != stepController.Step + 1)
            {
                Debug.LogError($"Failed step, tried to go from step {stepController.Step} to {Step}");
                return;
            }

            entityManager.SetComponentData(query.GetSingletonEntity(), new StepController(Step));
        }
        
        world.SetTime(new TimeData(Step*(double)Game.k_ClientPingFrequency, Game.k_ClientPingFrequency));
        
        // Apply extra actions
        {
            if (ExtraActionCount > 0)
            {
                Debug.Log($"Step {Step}: Applying {ExtraActionCount} extra actions");
                for (byte i = 0; i < ExtraActionCount; i++)
                    extraActionPtr[i].Apply(world);
            }
        }
        
        // Apply inputs
        {
            var query = entityManager.CreateEntityQuery(new ComponentType(typeof(StepInput)), new ComponentType(typeof(PlayerControlled)));
            var entities = query.ToEntityArray(Allocator.Temp);
            if (entities.Length > 0)
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var index = entityManager.GetSharedComponent<PlayerControlled>(entities[i]).Index;
                    var data = this[index];
                    entityManager.SetComponentData(entities[i], data);
                }
            }

            entities.Dispose();
        }
        
        
        world.Update();
    }
}