using System;
using BovineLabs.Saving;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[Save]
public struct Grounded : IComponentData, IEnableableComponent { }

[Save]
[Serializable]
public struct LockToSurface : IComponentData
{
    public float Height;
}

public struct RotateWithSurface : IComponentData { }

[Save]
[Serializable]
public struct PhysicsResponse : IComponentData
{
    public float Gravity;

    public float BounceThresholdMin;
    public float BounceResponse;
    public float BounceFriction;
    
    public float AirDrag;
    public float AirDragLinear;
    
    public float Friction;
    public float FrictionLinear;
    
    public float RotationalDrag;
    public float RotationalDragLinear;
}

[BurstCompile]
[UpdateInGroup(typeof(MovementSystemGroup))]
[UpdateAfter(typeof(MovementSystem))]
public partial struct TorusGravitySystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        new LockToSurfaceJob()
        {
        
        }.Schedule();
        new RotateWithSurfaceJob()
        {
            
        }.Schedule();
        new GravityJob()
        {
            dt = SystemAPI.Time.DeltaTime
        }.Schedule();
        new InertiaGravityJob()
        {
            dt = SystemAPI.Time.DeltaTime,
            randomIndex = unchecked((uint)SystemAPI.Time.ElapsedTime)
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
    
    [BurstCompile]
    [WithAll(typeof(RotateWithSurface))]
    partial struct RotateWithSurfaceJob : IJobEntity
    {
        public void Execute(ref LocalTransform transform)
        {
            var center = TorusMapper.GetTorusCircleCenter(transform.Position);
            
            var normal = transform.Position - center;
            var newForward = math.cross(transform.Right(), normal);
            var newRot = quaternion.LookRotationSafe(newForward, normal);
            transform.Rotation = newRot;
        }
    }
    
    [BurstCompile]
    [WithPresent(typeof(Grounded))]
    [WithNone(typeof(RotationalInertia))]
    partial struct GravityJob : IJobEntity
    {
        [ReadOnly] public float dt;
    
        public void Execute(ref Movement movement, ref LocalTransform transform, EnabledRefRW<Grounded> grounded, in PhysicsResponse response)
        {
            if (grounded.ValueRO)
            {
                TorusMapper.SnapToSurface(transform.Position, 0, out var newPos, out var newNormal);
                transform.Position = newPos;
                movement.Velocity -= math.dot(movement.Velocity, newNormal)*newNormal; // Zero vertical velocity
            }
            else
            {
                TorusMapper.ClampAboveSurface(transform.Position, out var clampedPos, out var clamped, out var normalIfClamped);
                if (clamped)
                {
                    // Zero velocity in clamp direction
                    var downVel = math.dot(movement.Velocity, -normalIfClamped);
                    downVel = math.max(downVel, 0); // Don't make any adjustment if they're already going up
                    
                    if (downVel > response.BounceThresholdMin)
                    {
                        // Bounce!
                        var bounceVel = downVel*response.BounceResponse;
                        movement.Velocity += (bounceVel + downVel)*normalIfClamped;
                        movement.Velocity -= movement.Velocity * response.BounceFriction;
                    }
                    else
                    {
                        // Remove vertical vel
                        movement.Velocity += downVel*normalIfClamped;
                        
                        // Grounding true
                        grounded.ValueRW = true;
                    }
                
                    // Snap position to surface
                    transform.Position = clampedPos;
                }
                else
                {
                    movement.Velocity -= normalIfClamped*response.Gravity*dt;
                }
            }
        }
    }
    
    [BurstCompile]
    [WithPresent(typeof(Grounded))]
    partial struct InertiaGravityJob : IJobEntity
    {
        [ReadOnly] public uint randomIndex;
        [ReadOnly] public float dt;
    
        public void Execute(Entity entity, ref Movement movement, ref LocalTransform transform, EnabledRefRW<Grounded> grounded, in PhysicsResponse response, ref RotationalInertia inertia)
        {
            if (grounded.ValueRO)
            {
                // Snap to surface
                TorusMapper.SnapToSurface(transform.Position, 0, out var newPos, out var newNormal);
                transform.Position = newPos;
                
                // Zero vertical velocity + inertia
                movement.Velocity -= math.dot(movement.Velocity, newNormal)*newNormal;
                inertia.Set(inertia.Normal, 0);
                
                // Lerp towards grounded rotation
                var newForward = math.cross(transform.Right(), newNormal);
                var newRot = quaternion.LookRotationSafe(newForward, newNormal);
                // This SHOULD rotate at a constant rate...
                var ang = math.angle(transform.Rotation, newRot);
                if (float.IsNormal(ang))
                    transform.Rotation = math.slerp(transform.Rotation, newRot, math.min(1.0f, dt*20 / ang));
            }
            else
            {
                TorusMapper.ClampAboveSurface(transform.Position, out var clampedPos, out var clamped, out var normalIfClamped);
                if (clamped)
                {
                    // Zero velocity in clamp direction
                    var downVel = math.dot(movement.Velocity, -normalIfClamped);
                    
                    if (downVel > response.BounceThresholdMin)
                    {
                        // Bounce!
                        var bounceVel = downVel*response.BounceResponse;
                        movement.Velocity += (bounceVel + downVel)*normalIfClamped;
                        movement.Velocity -= movement.Velocity * response.BounceFriction;
                        
                        // Randomise inertia
                        inertia.Set(Random.CreateFromIndex(unchecked((uint)(randomIndex + entity.Index + entity.Version))).NextFloat3(), downVel * response.BounceResponse);
                    }
                    else if (downVel >= 0)
                    {
                        // Remove vertical vel
                        movement.Velocity += downVel*normalIfClamped;
                        
                        // Grounding true
                        grounded.ValueRW = true;
                    }
                
                    // Snap position to surface
                    transform.Position = clampedPos;
                }
                else
                {
                    movement.Velocity -= math.normalizesafe(normalIfClamped)*response.Gravity*dt;
                }
            }
        }
    }
}