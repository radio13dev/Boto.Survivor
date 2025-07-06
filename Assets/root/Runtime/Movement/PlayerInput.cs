using System;
using System.Diagnostics;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine.InputSystem;

public struct PlayerInput : IInputComponentData
{
    public float2 Dir;
    public InputEvent Utility;
    public InputEvent Special1;
    public InputEvent Special2;
}

[UpdateInGroup(typeof(GhostInputSystemGroup))]
[AlwaysSynchronizeSystem]
public partial class GatherInputs : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<PlayerInput>();
    }

    protected override void OnUpdate()
    {
        new GatherJob()
        {
        }.Schedule();
    }

    [WithAll(typeof(GhostOwnerIsLocal))]
    partial struct GatherJob : IJobEntity
    {
        public void Execute(ref PlayerInput input)
        {
            float2 dir = float2.zero;
            if (Keyboard.current.wKey.isPressed) dir.y += 1;
            if (Keyboard.current.sKey.isPressed) dir.y -= 1;
            if (Keyboard.current.aKey.isPressed) dir.x -= 1;
            if (Keyboard.current.dKey.isPressed) dir.x += 1;
            input.Dir = dir;

            if (Keyboard.current.eKey.isPressed)
                input.Special1.Set();

            if (Keyboard.current.qKey.isPressed)
                input.Special2.Set();

            if (Keyboard.current.spaceKey.isPressed)
                input.Utility.Set();
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ThinClientSimulation)]
public partial class GatherInputs_ThinClient : SystemBase
{
    int m_FrameCount;
    uint m_WorldIndex;

    protected override void OnCreate()
    {
        RequireForUpdate<NetworkId>();

        // Give every thin client some randomness
        var rand = Unity.Mathematics.Random.CreateFromIndex((uint)Stopwatch.GetTimestamp());
        m_FrameCount = rand.NextInt(100);
        m_WorldIndex = UInt32.Parse(World.Name.Substring(World.Name.Length - 1));
    }

    protected override void OnUpdate()
    {
        // Check if the connection has no command target set yet, if not then create it (this is the dummy thin client player)
        if (SystemAPI.TryGetSingleton<CommandTarget>(out var commandTarget) && commandTarget.targetEntity == Entity.Null)
            CreateThinClientPlayer();

        byte left, right, up, down, jump;
        left = right = up = down = jump = 0;

        // Move in a random direction
        var state = (int)(SystemAPI.Time.ElapsedTime + m_WorldIndex) % 4;
        switch (state)
        {
            case 0: left = 1; break;
            case 1: right = 1; break;
            case 2: up = 1; break;
            case 3: down = 1; break;
        }

        // Jump every 100th frame
        if (++m_FrameCount % 100 == 0)
        {
            jump = 1;
            m_FrameCount = 0;
        }

        // Thin clients do not spawn anything so there will be only one PlayerInput component
        foreach (var inputData in SystemAPI.Query<RefRW<PlayerInput>>())
        {
            inputData.ValueRW = default;
            if (jump == 1)
                inputData.ValueRW.Utility.Set();
            if (left == 1)
                inputData.ValueRW.Dir.x -= 0.1f;
            if (right == 1)
                inputData.ValueRW.Dir.x += 0.1f;
            if (down == 1)
                inputData.ValueRW.Dir.y -= 0.1f;
            if (up == 1)
                inputData.ValueRW.Dir.y += 0.1f;
        }
    }

    void CreateThinClientPlayer()
    {
        // Create dummy entity to store the thin clients inputs
        // When using IInputComponentData the entity will need the input component and its generated
        // buffer, the GhostOwner set up with the local connection ID and finally the
        // CommandTarget needs to be manually set.
        var ent = EntityManager.CreateEntity();
        EntityManager.AddComponent<PlayerInput>(ent);

        var connectionId = SystemAPI.GetSingleton<NetworkId>().Value;
        EntityManager.AddComponentData(ent, new GhostOwner() { NetworkId = connectionId });
        EntityManager.AddComponent<InputBufferData<PlayerInput>>(ent);

        // NOTE: The server also has to manually set the command target for the thin client player
        // even though auto command target is used on the player prefab (and normal clients), see
        // SpawnPlayerSystem.
        SystemAPI.SetSingleton(new CommandTarget { targetEntity = ent });
    }
}