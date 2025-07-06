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
        new Job()
        {
            
        }.Schedule();
    }
    
    [WithAll(typeof(GhostOwnerIsLocal))]
    partial struct Job : IJobEntity
    {
        public void Execute(ref PlayerInput input)
        {
            float2 dir = float2.zero;
            if (Keyboard.current.wKey.isPressed) dir.y += 1;
            if (Keyboard.current.sKey.isPressed) dir.y -= 1;
            if (Keyboard.current.aKey.isPressed) dir.x -= 1;
            if (Keyboard.current.dKey.isPressed) dir.y += 1;
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