using System;
using BovineLabs.Saving;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[Save]
public struct Grounded : IComponentData, IEnableableComponent { }

[Save]
[Serializable]
public struct LockToSurface : IComponentData
{
    public float Height;
}

//[BurstCompile]
[UpdateInGroup(typeof(MovementSystemGroup))]
public partial struct TorusGravitySystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        new LockToSurfaceJob()
        {
        
        }.Schedule();
        new GravityJob()
        {
            dt = SystemAPI.Time.DeltaTime
        }.Schedule();
    }
    
    [BurstCompile]
    [WithAll(typeof(LockToSurface))]
    partial struct LockToSurfaceJob : IJobEntity
    {
        public void Execute(ref LocalTransform transform, in LockToSurface lockToSurface)
        {
            TorusMapper.SnapToSurface(transform.Position, lockToSurface.Height, out var newPos, out var newNormal);
            
            var newForward = math.cross(transform.Right(), newNormal);
            var newRot = quaternion.LookRotationSafe(newForward, newNormal);
            transform = LocalTransform.FromPositionRotation(newPos, newRot);
        }
    }
    
    //[BurstCompile]
    [WithNone(typeof(LockToSurface))]
    [WithPresent(typeof(Grounded))]
    partial struct GravityJob : IJobEntity
    {
        [ReadOnly] public float dt;
    
        public void Execute(ref Movement movement, ref LocalTransform transform, EnabledRefRW<Grounded> grounded)
        {
            if (grounded.ValueRO)
            {
                Debug.DrawLine(float3.zero, TorusMapper.GetTorusCircleCenter(transform.Position), Color.blue, 0.1f);
                TorusMapper.SnapToSurface(transform.Position, 0, out var newPos, out var newNormal);
                
                var newForward = math.cross(transform.Right(), newNormal);
                var newRot = quaternion.LookRotationSafe(newForward, newNormal);
                transform = LocalTransform.FromPositionRotation(newPos, newRot);
            }
            else
            {
                TorusMapper.ClampAboveSurface(transform.Position, out var clampedPos, out var clamped, out var normalIfClamped);
                if (clamped)
                {
                    // Grounding true
                    grounded.ValueRW = true;
                
                    // Zero velocity in clamp direction
                    movement.Velocity -= math.dot(movement.Velocity, normalIfClamped)*normalIfClamped;
                
                    // Snap position to surface
                    transform.Position = clampedPos;
                }
                else
                {
                    movement.Velocity -= normalIfClamped*dt;
                }
                
                // Adjust rotation
                var newForward = math.cross(transform.Right(), normalIfClamped);
                var newRot = quaternion.LookRotationSafe(newForward, normalIfClamped);
                transform.Rotation = newRot;
            }
        }
    }
}