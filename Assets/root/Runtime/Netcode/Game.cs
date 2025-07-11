using System;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Networking.Transport.Samples;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class Game : ICustomBootstrap
{
    public static World World { get; private set; }

    public bool Initialize(string defaultWorldName)
    {
        World.DefaultGameObjectInjectionWorld = CreateWorld();
        return true;
    }
    
    public static World CreateWorld()
    {
        Debug.Log($"Creating Game...");
        World = new World("Game");
        var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation);
        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World, systems);
        return World;
    }
}

public partial struct StepSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.EntityManager.CreateSingleton<StepController>();
    }
}

public struct StepController : IComponentData
{
    public long Step;

    public StepController(long step)
    {
        Step = step;
    }
}

public struct PlayerControlledTag : IComponentData { }

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
            if ((Input & LeftInput) > 0) direction += new float2(-1,0);
            if ((Input & RightInput) > 0) direction += new float2(1,0);
            if ((Input & UpInput) > 0) direction += new float2(0,1);
            if ((Input & DownInput) > 0) direction += new float2(0,-1);
            return math.normalizesafe(direction);
        }
    }
    
    public bool S1 => (Input & S1Input) > 0;
}