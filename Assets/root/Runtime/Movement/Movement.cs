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
public struct Movement : IComponentData
{
    public float3 Velocity;
    public float3 LastDirection;
}

[Save]
public struct SurfaceMovement : IComponentData
{
    public float2 Velocity;
}

[UpdateInGroup(typeof(MovementSystemGroup))]
public partial struct MovementSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        new MovementJob()
        {
            dt = SystemAPI.Time.DeltaTime
        }.Schedule();
        new SurfaceMovementJob()
        {
            dt = SystemAPI.Time.DeltaTime
        }.Schedule();
    }
    
    partial struct MovementJob : IJobEntity
    {
        [ReadOnly] public float dt;
    
        public void Execute(in Movement movement, ref LocalTransform transform)
        {
            transform.Position += movement.Velocity*dt;
        }
    }
    
    partial struct SurfaceMovementJob : IJobEntity
    {
        [ReadOnly] public float dt;

        public void Execute(in SurfaceMovement surfaceMovement, ref LocalTransform transform)
        {
            transform.Position += transform.TransformDirection(surfaceMovement.Velocity.f3z())*dt;
        }
    }
}