using System.Runtime.InteropServices;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class StepInputAuthoring : MonoBehaviour
{
    public MovementSettings MovementSettings = new MovementSettings()
    {
        Speed = 1
    };
    
    partial class Baker : Baker<StepInputAuthoring>
    {
        public override void Bake(StepInputAuthoring authoring)
        {
            if (!DependsOn(GetComponent<PhysicsAuthoring>()))
            {
                Debug.LogError($"Cannot bake {authoring.gameObject}. Requires component: {nameof(PhysicsAuthoring)}");
                return;
            }
        
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, authoring.MovementSettings);
            AddComponent<StepInput>(entity);
        }
    }
}

[Save]
[StructLayout(LayoutKind.Explicit, Size = StepInput.Length, Pack = 4)]
public unsafe struct StepInput : IComponentData
{
    public const int Length = sizeof(long)*2;
    [SerializeField] [FieldOffset(0)] long m_WRITE0;
    [SerializeField] [FieldOffset(8)] long m_WRITE1;
    
    [FieldOffset(0)] public byte Input;
    [FieldOffset(4)] public float3 Direction;
        
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
        writer.WriteLong(m_WRITE0);
        writer.WriteLong(m_WRITE1);
    }

    public static StepInput Read(ref DataStreamReader reader)
    {
        return new StepInput(){m_WRITE0 = reader.ReadLong(), m_WRITE1 = reader.ReadLong()};
    }

    public void Collect(Camera camera)
    {
        if (!camera) return;

        var player = GameInput.Inputs.Player;
        var dir = player.Move.ReadValue<Vector2>();
        Direction += (float3)camera.transform.right*dir.x + (float3)camera.transform.up*dir.y;
        
        if (Keyboard.current.jKey.isPressed    )    Input |= StepInput.S1Input;
        if (Keyboard.current.kKey.isPressed    )    Input |= StepInput.S2Input;
        if (Keyboard.current.spaceKey.isPressed)    Input |= StepInput.S3Input;
        if (Keyboard.current.shiftKey.isPressed)    Input |= StepInput.S4Input;
    }

    public override string ToString()
    {
        return $"{Input}:{Direction}";
    }
}