using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[StructLayout(layoutKind: LayoutKind.Sequential)]
public struct StepInput : IComponentData
{
    public byte Input;

    public StepInput(byte input)
    {
        Input = input;
    }

    public const int SizeOf = 1;
        
        // @formatter:off
        public const byte LeftInput     = 0b0000_0001;
        public const byte RightInput    = 0b0000_0010;
        public const byte UpInput       = 0b0000_0100;
        public const byte DownInput     = 0b0000_1000;
        
        public const byte S1Input       = 0b0001_0000;
        public const byte S2Input       = 0b0010_0000;
        public const byte S3Input       = 0b0100_0000;
        public const byte S4Input       = 0b1000_0000;
    // @formatter:on 

    public float2 Direction
    {
        get
        {
            float2 direction = default;
            if ((Input & LeftInput) > 0) direction += new float2(-1, 0);
            if ((Input & RightInput) > 0) direction += new float2(1, 0);
            if ((Input & UpInput) > 0) direction += new float2(0, 1);
            if ((Input & DownInput) > 0) direction += new float2(0, -1);
            return math.normalizesafe(direction);
        }
    }

    public bool S1 => (Input & S1Input) > 0;

    public void Write(ref DataStreamWriter writer)
    {
        writer.WriteByte(Input);
    }

    public static StepInput Read(ref DataStreamReader reader)
    {
        return new StepInput(reader.ReadByte());
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
            this[i] = new StepInput(connections[i].InputBuffer.Input);
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

            if (stepController.Step != Step - 1)
            {
                Debug.LogError($"Failed step, tried to go from step {stepController.Step} to {Step}");
                return;
            }

            entityManager.SetComponentData(query.GetSingletonEntity(), new StepController(Step));
        }
        
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
                var components = query.ToComponentDataArray<PlayerControlled>(Allocator.Temp);
                for (int i = 0; i < components.Length; i++)
                {
                    var data = this[components[i].Index];
                    entityManager.SetComponentData(entities[i], data);
                }

                components.Dispose();
            }

            entities.Dispose();
        }
        
        
        world.Update();
    }
}