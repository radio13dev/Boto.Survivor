using BovineLabs.Saving;
using Collisions;
using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[Save]
public struct ProjectileLoopTrigger : IComponentData
{
    public const byte k_MaxLoopCount = 4;

    public byte PlayerId;
    public byte RingIndex;
    public byte LoopCount;
    
    public ProjectileLoopTrigger(byte loopCount, byte playerId, byte ringIndex)
    {
        LoopCount = loopCount;
        PlayerId = playerId;
        RingIndex = ringIndex;
    }

    public static readonly ProjectileLoopTrigger Empty = new ProjectileLoopTrigger(byte.MaxValue, 0, 0);
}

[Save]
public struct ProjectileLoopTriggerQueue : IBufferElementData
{
    public byte RingIndex;
    public byte LoopCount;
    
    public LocalTransform HitT;
    public SurfaceMovement ProjSurfaceMov;

    public ProjectileLoopTriggerQueue(ProjectileHit hit, LocalTransform hitT, ProjectileLoopTrigger trigger, SurfaceMovement projSurfaceMov)
    {
        RingIndex = trigger.RingIndex;
        LoopCount = (byte)(trigger.LoopCount + 1);
        HitT = hitT;
        ProjSurfaceMov = projSurfaceMov;
    }
}

[UpdateInGroup(typeof(ProjectileSystemGroup))]
[BurstCompile]
[RequireMatchingQueriesForUpdate]
public partial struct ProjectileHitSystem_LoopTrigger : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerControlledLink>();
    }
    
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new Job()
        {
            Players = SystemAPI.GetSingletonBuffer<PlayerControlledLink>(true),
            LoopQueueLookup = SystemAPI.GetBufferLookup<ProjectileLoopTriggerQueue>(false),
            TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true)
        }.Schedule(state.Dependency);
    }
    
    [WithAll(typeof(ProjectileHit))]
    partial struct Job : IJobEntity
    {
        [ReadOnly] public DynamicBuffer<PlayerControlledLink> Players;
        public BufferLookup<ProjectileLoopTriggerQueue> LoopQueueLookup;
        [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    
        public void Execute(in LocalTransform projT, in ProjectileLoopTrigger loop, in SurfaceMovement projSurfaceMov, in DynamicBuffer<ProjectileHitEntity> hits)
        {
            // Early max loops hit escape
            if (loop.LoopCount >= ProjectileLoopTrigger.k_MaxLoopCount) return;
            if (loop.PlayerId < 0 || loop.PlayerId >= Players.Length) return;
            
            // Get the player
            if (!LoopQueueLookup.TryGetBuffer(Players[loop.PlayerId].Value, out var queue)) return;
            
            LocalTransform avgT = projT;
            avgT.Position = 0;
            for (int i = 0; i < hits.Length; i++)
            {
                // Get the information about the entity we hit
                if (!TransformLookup.TryGetComponent(hits[i].Value, out var hitT))
                {
                    avgT.Position += projT.Position/hits.Length;
                    continue;
                }
                avgT.Position += hitT.Position/hits.Length;
            }
            
            // Queue this hit up to get processed by the players projectile generation step
            queue.Add(new ProjectileLoopTriggerQueue(default, avgT, loop, projSurfaceMov));
        }
    }
}