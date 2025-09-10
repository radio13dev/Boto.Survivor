using System;
using BovineLabs.Saving;
using Drawing;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class PhysicsAuthoring : MonoBehaviourGizmos
{
    public PhysicsResponse PhysicsResponse = PhysicsResponse.Default;
    public bool HasSurfaceMovement;
    
    partial class Baker : Baker<PhysicsAuthoring>
    {
        public override void Bake(PhysicsAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, authoring.PhysicsResponse);
            AddComponent<Movement>(entity);
            AddComponent(entity, new Grounded());
            SetComponentEnabled<Grounded>(entity, false);
            AddComponent<RotationalInertia>(entity);
            AddComponent<Force>(entity);
            
            if (authoring.HasSurfaceMovement)
                AddComponent<SurfaceMovement>(entity);
        }
    }

    public override void DrawGizmos()
    {
        using (Draw.WithLineWidth(2, false))
        {
            Draw.WireSphere(transform.position, PhysicsResponse.Radius, new Color(1f, 0f, 1, 0.5f));
        }
    }
}

/// <summary>
/// Character movement is predicted
/// </summary>
[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
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
    public float3 PerFrameVelocity;
}

[Save]
public struct Force : IComponentData
{
    public float3 Velocity;
    public float3 Shift;
    
    public void Reset()
    {
        Velocity = default;
        Shift = default;
    }
}

[Save]
public struct RotationalInertia : IComponentData
{
    public float3 Normal;
    public float Rate;
    public quaternion GetRotation(float dt) => math.normalizesafe(quaternion.AxisAngle(Normal, Rate*dt));
    
    public void Set(float3 normal, float rate)
    {
        Normal = normal;
        Rate = rate;
    }
}

[Save]
public struct Grounded : IComponentData, IEnableableComponent { }

[Save]
public struct PhysicsResponseRef : IComponentData
{
    public int ResponseIndex;

    public PhysicsResponse Get()
    {
        // TODO: Implement this to save memory per entity
        return PhysicsResponse.Default;
    }
}

[Save]
[Serializable]
public struct PhysicsResponse : IComponentData
{
    public float Gravity;

    public float BounceThresholdMin;
    public float BounceResponse;
    public float BounceFriction;
    public bool DisableGrounding;
    
    public float AirDrag;
    public float AirDragLinear;
    
    public float Friction;
    public float FrictionLinear;
    
    public float RotationalDrag;
    public float RotationalDragLinear;
    
    public float Radius;
    public bool LockToSurface;
    public bool RotateWithSurface;
    public float3 IdlePivot;

    public float ForceVelocityResistance;
    public float ForceShiftResistance;
    public bool LookInMoveDirection;
    
    
    public static readonly PhysicsResponse Default = new PhysicsResponse()
    {
        Gravity = 30,
        BounceThresholdMin = 100,
        BounceResponse = 0.5f,
        BounceFriction = 0.5f,
        
        AirDrag = 1f,
        AirDragLinear = 1f,
        
        Friction = 1f,
        FrictionLinear = 1f,
        
        RotationalDrag = 1f,
        RotationalDragLinear = 1f,
        
        Radius = 1f,
        
        LockToSurface = true,
        RotateWithSurface = true,
        IdlePivot = default
    };
    
    public static readonly PhysicsResponse Stationary = new PhysicsResponse()
    {
        Gravity = 30,
        BounceThresholdMin = 100,
        BounceResponse = 0.5f,
        BounceFriction = 0.5f,
        
        AirDrag = 1000f,
        AirDragLinear = 1000f,
        
        Friction = 1000f,
        FrictionLinear = 1000f,
        
        RotationalDrag = 1000f,
        RotationalDragLinear = 1000f,
        
        Radius = 0f,
        
        LockToSurface = true,
        RotateWithSurface = true,
        IdlePivot = default,
        
        ForceVelocityResistance = 1,
        ForceShiftResistance = 1,
    };
}

[BurstCompile]
[UpdateInGroup(typeof(MovementSystemGroup))]
public partial struct TorusGravitySystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
    }

    public void OnUpdate(ref SystemState state)
    {
        var a = new TestJob()
        {
            dt = SystemAPI.Time.DeltaTime
        }.ScheduleParallel(state.Dependency);
        var b = new RefJob()
        {
            dt = SystemAPI.Time.DeltaTime
        }.ScheduleParallel(state.Dependency);
        state.Dependency = JobHandle.CombineDependencies(a,b);
        state.Dependency = new SurfaceMovementJob()
        {
            
        }.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    [WithAll(typeof(SurfaceMovement))]
    partial struct SurfaceMovementJob : IJobEntity
    {
        public void Execute(in LocalTransform transform, ref Movement movement, in SurfaceMovement surfaceMovement)
        {
            // Surface movement
            var surfaceDir = transform.TransformDirection(surfaceMovement.PerFrameVelocity);
            movement.Velocity = surfaceDir;
            movement.LastDirection = math.normalizesafe(surfaceDir, movement.LastDirection);
        }
    }
    
    [BurstCompile]
    [WithPresent(typeof(Grounded))]
    partial struct RefJob : IJobEntity
    {
        [ReadOnly] public float dt;
        public void Execute(ref LocalTransform transform, ref Movement movement, ref Force force, ref RotationalInertia inertia, EnabledRefRW<Grounded> grounded, in PhysicsResponseRef physicsResponseRef)
        {
            var r = physicsResponseRef.Get();
            new TestJob()
            {
                dt = dt
            }.Execute(ref transform, ref movement, ref force, ref inertia, grounded, in r);
        }
    }
    
    [BurstCompile]
    [WithPresent(typeof(Grounded))]
    partial struct TestJob : IJobEntity
    {
        [ReadOnly] public float dt;
    
        [BurstCompile]
        public void Execute(ref LocalTransform transform, ref Movement movement, ref Force force, ref RotationalInertia inertia, EnabledRefRW<Grounded> grounded, in PhysicsResponse physicsResponse)
        {
            // Force
            movement.Velocity += force.Velocity * (1.0f - physicsResponse.ForceVelocityResistance);
            transform.Position += force.Shift * (1.0f - physicsResponse.ForceShiftResistance);
            
            force = new();
            
            // Movement
            transform.Position += movement.Velocity*dt;
            
            if (physicsResponse.LookInMoveDirection && math.any(movement.Velocity != 0))
                transform.Rotation = quaternion.LookRotationSafe(movement.Velocity, transform.Up());
            
            float3 surfaceNormal;
            // Gravity
            if (grounded.ValueRO || physicsResponse.LockToSurface)
            {
                if (!grounded.ValueRO) grounded.ValueRW = true;

                TorusMapper.SnapToSurface(transform.Position, physicsResponse.Radius, out var newPos, out surfaceNormal);
                transform.Position = newPos;
                movement.Velocity -= math.dot(movement.Velocity, surfaceNormal) * surfaceNormal; // Zero vertical velocity

                // Relative drag
                if (physicsResponse.Friction != 0) movement.Velocity -= movement.Velocity * dt * physicsResponse.Friction;
                // Linear drag
                if (physicsResponse.FrictionLinear != 0) movement.Velocity = mathu.MoveTowards(movement.Velocity, float3.zero, physicsResponse.FrictionLinear * dt);
            }
            else
            {
                TorusMapper.ClampAboveSurface(transform.Position, physicsResponse.Radius, out var clampedPos, out var clamped, out var normalIfClamped);
                if (clamped)
                {
                    surfaceNormal = normalIfClamped;

                    // Zero velocity in clamp direction
                    var downVel = math.dot(movement.Velocity, -normalIfClamped);
                    var downVelVec = downVel*normalIfClamped;

                    if (downVel < 0 || (physicsResponse.BounceThresholdMin > 0 && downVel > physicsResponse.BounceThresholdMin))
                    {
                        if (downVel > 0)
                        {
                            // Zero vertical
                            movement.Velocity += downVelVec;
                    
                            // Hit ground friction
                            movement.Velocity -= movement.Velocity * physicsResponse.BounceFriction;
                    
                            // Bounce!
                            var bounceVelVec = downVelVec * physicsResponse.BounceResponse;
                            movement.Velocity += bounceVelVec;
                        }

                        // Randomise inertia
                        if (!physicsResponse.RotateWithSurface)
                        {
                            float tangentialSpeed = math.length(movement.Velocity) / physicsResponse.Radius;
                            tangentialSpeed *= 3;
                            inertia.Set(math.cross(surfaceNormal, math.normalizesafe(movement.Velocity)), tangentialSpeed);
                        }
                    }
                    else
                    {
                        // Remove vertical vel
                        movement.Velocity += downVelVec;

                        // Grounding true
                        if (!physicsResponse.DisableGrounding)
                            grounded.ValueRW = true;
                    }
                    
                    // Snap position to surface
                    transform.Position = clampedPos;
                }
                else
                {
                    surfaceNormal = math.normalizesafe(normalIfClamped);

                    movement.Velocity -= surfaceNormal * physicsResponse.Gravity * dt;

                    // Relative drag
                    if (physicsResponse.AirDrag != 0) movement.Velocity -= movement.Velocity * dt * physicsResponse.AirDrag;
                    // Linear drag
                    if (physicsResponse.AirDragLinear != 0) movement.Velocity = mathu.MoveTowards(movement.Velocity, float3.zero, physicsResponse.AirDragLinear * dt);
                }
            }
            
            // Most objects rotate with surface
            if (physicsResponse.RotateWithSurface)
            {
                transform.Rotation = quaternion.LookRotationSafe(math.cross(surfaceNormal, math.cross(transform.Forward(), surfaceNormal)), surfaceNormal);
            }
            // Roll with surface
            else if (grounded.ValueRO)
            {
                // Calculate the tangential velocity based on the object's velocity and radius
                float tangentialSpeed = math.length(movement.Velocity) / physicsResponse.Radius;
                
                if (tangentialSpeed == 0)
                {
                    if (physicsResponse.IdlePivot.x != 0) // TODO: Finish idle pivot impl.
                    {
                        // Lerp towards grounded rotation
                        var newForward = math.cross(transform.Right(), surfaceNormal);
                        var newRot = quaternion.LookRotationSafe(newForward, surfaceNormal);
                        var ang = math.angle(transform.Rotation, newRot);
                        if (float.IsNormal(ang))
                            transform.Rotation = math.slerp(transform.Rotation, newRot, math.min(1.0f, dt * 10 / ang));
                    }
                }
                else
                {
                    // Set the final rate proportional to the tangential speed
                    inertia.Set(math.cross(surfaceNormal, math.normalizesafe(movement.Velocity)), tangentialSpeed);
                    transform.Rotation = math.normalizesafe(math.mul(inertia.GetRotation(dt), transform.Rotation));
                }
            }
            else if (inertia.Rate != 0)
            {
                var finalRate = inertia.Rate;
                if (physicsResponse.RotationalDrag != 0) finalRate -= finalRate * physicsResponse.RotationalDrag * dt;
                if (physicsResponse.RotationalDragLinear != 0) finalRate = mathu.MoveTowards(finalRate, 0, physicsResponse.RotationalDragLinear*dt);
                inertia.Set(inertia.Normal, finalRate);
                transform.Rotation = math.normalizesafe(math.mul(inertia.GetRotation(dt), transform.Rotation));
            }
        }
    }
}