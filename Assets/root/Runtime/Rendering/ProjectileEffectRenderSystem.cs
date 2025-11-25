using System;
using Collisions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
[UpdateInGroup(typeof(RenderSystemGroup))]
[RequireMatchingQueriesForUpdate]
public unsafe partial struct ProjectileEffectRenderSystem : ISystem
{
    NativeArray<Matrix4x4> m_InstanceMats;
    EntityQuery m_ChainQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameManager.InstancedResources>();
        state.RequireForUpdate<RenderSystemHalfTime>();
        m_InstanceMats = new NativeArray<Matrix4x4>(Profiling.k_MaxRender, Allocator.Persistent);
        
        m_ChainQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Chain>().Build();
    }

    public void OnDestroy(ref SystemState state)
    {
        m_InstanceMats.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        var halfTime = SystemAPI.GetSingleton<RenderSystemHalfTime>();
        var resources = SystemAPI.GetSingletonBuffer<GameManager.InstancedResources>();
        
        //for (int resourceIt = 0; resourceIt < resources.Length; resourceIt++)
        {
        //    if (!resources[resourceIt].Valid) continue;
            if (m_ChainQuery.IsEmpty) return;
            
            var resource = resources[GameManager.InstancedResources.ChainEffectIndex].Instance.Value;
            
            NativeArray<LocalTransform> transforms = m_ChainQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            int toRender = math.min(transforms.Length, m_InstanceMats.Length); 

            NativeArray<LocalTransformLast> transformsLast = default;
            
            if (resource.UseLastTransform)
            {
                transformsLast = m_ChainQuery.ToComponentDataArray<LocalTransformLast>(Allocator.TempJob);
                LightweightRenderSystem.AsyncRenderTransformGenerator asyncRenderTransformGenerator = new LightweightRenderSystem.AsyncRenderTransformGenerator
                {
                    transforms = transforms,
                    transformsLast = transformsLast,
                    matrices = m_InstanceMats,
                    t = halfTime.Value
                };
                asyncRenderTransformGenerator.ScheduleParallel(toRender, 64, default).Complete();
            }
            else
            {
                LightweightRenderSystem.AsyncRenderTransformGeneratorLightweight asyncRenderTransformGenerator = new LightweightRenderSystem.AsyncRenderTransformGeneratorLightweight
                {
                    transforms = transforms,
                    matrices = m_InstanceMats,
                };
                asyncRenderTransformGenerator.ScheduleParallel(toRender, 64, default).Complete();
            }
            
            var mesh = resource.Mesh;
            var renderParams = resource.RenderParams;
            
            for (int j = 0; j < toRender; j += Profiling.k_MaxInstances)
            {
                int count = math.min(Profiling.k_MaxInstances, toRender - j);
                Graphics.RenderMeshInstanced(renderParams, mesh, 0, m_InstanceMats, count, j);
            }
            
            transforms.Dispose();
            
            if (transformsLast.IsCreated) transformsLast.Dispose();
        }
    }
}


[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
[UpdateInGroup(typeof(RenderSystemGroup))]
[RequireMatchingQueriesForUpdate]
public unsafe partial struct ParticleEffectRenderSystem : ISystem
{
    NativeArray<Matrix4x4> m_InstanceMats;
    EntityQuery m_ChainQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameManager.InstancedResources>();
        m_InstanceMats = new NativeArray<Matrix4x4>(Profiling.k_MaxRender, Allocator.Persistent);
        
        m_ChainQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Chain.Visual, SpawnTimeCreated, DestroyAtTime>().Build();
    }

    public void OnDestroy(ref SystemState state)
    {
        m_InstanceMats.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        var resources = SystemAPI.GetSingletonBuffer<GameManager.InstancedResources>();
        
        //for (int resourceIt = 0; resourceIt < resources.Length; resourceIt++)
        {
        //    if (!resources[resourceIt].Valid) continue;
            if (m_ChainQuery.IsEmpty) return;
            
            var resourceIndex = GameManager.InstancedResources.ChainVisualIndex;
            var resource = resources[resourceIndex].Instance.Value;
            var query = state.WorldUnmanaged.GetUnsafeSystemRef<LightweightRenderSystem>(state.WorldUnmanaged.GetExistingUnmanagedSystem<LightweightRenderSystem>())
                .m_InstanceQueries[resourceIndex];
            
            NativeArray<LocalTransform> transforms = m_ChainQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            NativeArray<Chain.Visual> stretch = m_ChainQuery.ToComponentDataArray<Chain.Visual>(Allocator.Temp);
            int toRender = math.min(transforms.Length, m_InstanceMats.Length); 

            
            LightweightRenderSystem.AsyncRenderTransformGeneratorLightweight asyncRenderTransformGenerator = new LightweightRenderSystem.AsyncRenderTransformGeneratorLightweight
            {
                transforms = transforms,
                matrices = m_InstanceMats,
            };
            asyncRenderTransformGenerator.ScheduleParallel(toRender, 64, default).Complete();
            
            NativeArray<float> lifespan = default;
            //if (resource.HasLifespan)
            {
                var destroyAtTime = query.ToComponentDataArray<DestroyAtTime>(Allocator.Temp).Reinterpret<double>();
                var spawnAtTime = query.ToComponentDataArray<SpawnTimeCreated>(Allocator.Temp).Reinterpret<double>();
                lifespan = new NativeArray<float>(destroyAtTime.Length, Allocator.Temp);
                for (int lifeIt = 0; lifeIt < destroyAtTime.Length; lifeIt++)
                    lifespan[lifeIt] = math.clamp((float)((SystemAPI.Time.ElapsedTime - spawnAtTime[lifeIt])/(destroyAtTime[lifeIt] - spawnAtTime[lifeIt])), 0, 1);
                destroyAtTime.Dispose();
                spawnAtTime.Dispose();
            }
            
            var mesh = resource.Mesh;
            var renderParams = resource.RenderParams;
            
            for (int j = 0; j < toRender; j += Profiling.k_MaxInstances)
            {
                int count = math.min(Profiling.k_MaxInstances, toRender - j);
                
                {
                    renderParams.matProps.SetFloatArray("lifespanBuffer", new Span<float>(&((float*)lifespan.GetUnsafePtr())[j], count).ToArray());
                }
                {
                    renderParams.matProps.SetVectorArray("stretchBuffer", new Span<Vector4>(&((Vector4*)stretch.GetUnsafePtr())[j], count).ToArray());
                }
                Graphics.RenderMeshInstanced(renderParams, mesh, 0, m_InstanceMats, count, j);
            }
            
            transforms.Dispose();
            stretch.Dispose();
            lifespan.Dispose();
        }
    }
}