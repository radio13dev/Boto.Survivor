using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = UnityEngine.Random;

public struct LocalTransformLast : IComponentData
{
    public LocalTransform Value;
}

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
[UpdateBefore(typeof(LightweightRenderSystem))]
[UpdateInGroup(typeof(RenderSystemGroup))]
public partial struct LocalTransformLastSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        new UpdateLastTransform().Schedule();
    }

    [BurstCompile]
    partial struct UpdateLastTransform : IJobEntity
    {
        public void Execute(ref LocalTransformLast last, in LocalTransform current)
        {
            last.Value = current;
        }
    }
}

public struct RenderSystemHalfTime : IComponentData
{
    public float Value;
}

[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial class RenderSystemGroup : ComponentSystemGroup
{
    protected override void OnCreate()
    {
        base.OnCreate();
        EntityManager.CreateEntity(typeof(RenderSystemHalfTime));
    }
}

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
[UpdateInGroup(typeof(RenderSystemGroup))]
public unsafe partial struct LightweightRenderSystem : ISystem
{
    EntityQuery m_Query;
    NativeArray<Matrix4x4> m_InstanceMats;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameManager.Resources>();
        state.RequireForUpdate<RenderSystemHalfTime>();
        m_Query = SystemAPI.QueryBuilder().WithAll<LocalTransform, LocalTransformLast, InstancedResourceRequest, SpriteAnimFrame>().Build();
        m_InstanceMats = new NativeArray<Matrix4x4>(Profiling.k_MaxRender, Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        m_InstanceMats.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        var halfTime = SystemAPI.GetSingleton<RenderSystemHalfTime>();
        var resources = SystemAPI.GetSingletonBuffer<GameManager.InstancedResources>();
        
        for (int i = 0; i < resources.Length; i++)
        {
            m_Query.SetSharedComponentFilter(new InstancedResourceRequest(i));
            var transforms = m_Query.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            if (transforms.Length == 0)
            {
                transforms.Dispose();
                continue;
            }
            var transformsLast = m_Query.ToComponentDataArray<LocalTransformLast>(Allocator.TempJob);
            var spriteIndices = m_Query.ToComponentDataArray<SpriteAnimFrame>(Allocator.Temp);
            var spriteIndicesF = spriteIndices.Reinterpret<float>();
            
            int toRender = math.min(transforms.Length, m_InstanceMats.Length); 
            AsyncRenderTransformGenerator asyncRenderTransformGenerator = new AsyncRenderTransformGenerator
            {
                transforms = transforms,
                transformsLast = transformsLast,
                matrices = m_InstanceMats,
                t = halfTime.Value
            };
            asyncRenderTransformGenerator.ScheduleParallel(toRender, 64, default).Complete();
            
            var resource = resources[i].Instance.Value;
            var mesh = resource.Mesh;
            var renderParams = resource.RenderParams;
            
            for (int j = 0; j < toRender; j += Profiling.k_MaxInstances)
            {
                int count = math.min(Profiling.k_MaxInstances, toRender - j);
            
                if (resource.Animated)
                {
                    renderParams.matProps.SetFloatArray("spriteAnimFrameBuffer", new Span<float>(&((float*)spriteIndicesF.GetUnsafePtr())[j], count).ToArray());
                }
                
                Graphics.RenderMeshInstanced(renderParams, mesh, 0, m_InstanceMats, count, j);
            }
            
            transforms.Dispose();
            transformsLast.Dispose();
            spriteIndices.Dispose();
        }
    }

    [BurstCompile]
    partial struct AsyncRenderTransformGenerator : IJobFor
    {
        [ReadOnly] public float t;
        [ReadOnly] public NativeArray<LocalTransformLast> transformsLast;
        [ReadOnly] public NativeArray<LocalTransform> transforms;

        [WriteOnly] public NativeArray<Matrix4x4> matrices;

        [BurstCompile]
        public void Execute(int index)
        {
            var oldTransform = transformsLast[index];
            var newTransform = transforms[index];
            var p = math.lerp(oldTransform.Value.Position, newTransform.Position, t);
            var q = math.slerp(oldTransform.Value.Rotation, newTransform.Rotation, t);
            matrices[index] = LocalTransform.FromPositionRotation(p, q).ToMatrix();
        }
    }
}
