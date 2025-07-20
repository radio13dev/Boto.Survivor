using System;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Character movement is predicted
/// </summary>
public partial class MovementSystemGroup : ComponentSystemGroup
{
        
}

[Save]
[Serializable]
public struct MovementSettings : IComponentData
{
    [SerializeField] public float Speed;
}

[Save]
[Serializable]
public struct DragSettings : IComponentData
{
    [SerializeField] public float Drag;
    [SerializeField] public float LinearDrag;
}

[Save]
public struct Grounded : IComponentData, IEnableableComponent { }

[Save]
[Serializable]
public struct Movement : IComponentData
{
    [HideInInspector] public float3 Velocity;
    [HideInInspector] public float3 LastDirection;
}

[Save]
[Serializable]
public struct LockToSurface : IComponentData
{
    public float Height;
}

[UpdateInGroup(typeof(MovementSystemGroup))]
public partial struct MovementSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        new Job()
        {
            dt = SystemAPI.Time.DeltaTime
        }.Schedule();
    }
    
    partial struct Job : IJobEntity
    {
        [ReadOnly] public float dt;
    
        public void Execute(Entity entity, ref Movement movement, ref LocalTransform transform)
        {
            transform.Position += movement.Velocity*dt;
            
            // Relative drag
            if (movement.Drag != 0) movement.Velocity -= movement.Velocity*dt*movement.Drag;
            // Linear drag
            if (movement.LinearDrag != 0) movement.Velocity = mathu.MoveTowards(movement.Velocity, float3.zero, movement.LinearDrag*dt);
        }
    }
}