using System;
using BovineLabs.Saving;
using Collisions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Collider = Collisions.Collider;

public struct Hidden : IComponentData
{
    
}

public struct LocalTransformLast : IComponentData
{
    public LocalTransform Value;
}

[BurstCompile]
[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct LocalTransformLastSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        new UpdateLastTransform().Schedule();
    }

    [BurstCompile]
    [WithNone(typeof(Hidden))]
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
    bool m_Init;
    NativeArray<Matrix4x4> m_InstanceMats;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameManager.InstancedResources>();
        state.RequireForUpdate<RenderSystemHalfTime>();
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
        
        if (!m_Init)
        {
            m_Init = true;
            for (int resourceIt = 0; resourceIt < resources.Length; resourceIt++)
            {
                var resource = resources.ElementAt(resourceIt);
                if (!resource.Valid) continue;
                
                var resourceData = resource.Instance.Value;
                var queryBuilder = new EntityQueryBuilder(Allocator.Persistent).WithAll<LocalTransform, InstancedResourceRequest>().WithNone<Hidden>();
                
                if (resourceData.UseLastTransform) queryBuilder = queryBuilder.WithAll<LocalTransformLast>();
                if (resourceData.Animated) queryBuilder = queryBuilder.WithAll<SpriteAnimFrame>();
                if (resourceData.HasLifespan) queryBuilder = queryBuilder.WithAll<SpawnTimeCreated, DestroyAtTime>();
                if (resourceData.IsTorus) queryBuilder = queryBuilder.WithAll<TorusMin>();
                if (resourceData.IsCone) queryBuilder = queryBuilder.WithAll<TorusCone>();
                
                resourceData.Query = queryBuilder.Build(ref state);
            }
        }

        for (int resourceIt = 0; resourceIt < resources.Length; resourceIt++)
        {
            if (!resources[resourceIt].Valid) continue;
            
            var resource = resources[resourceIt].Instance.Value;
            resource.Query.SetSharedComponentFilter(new InstancedResourceRequest(resourceIt));
            if (resource.Query.IsEmpty)
                continue;
            
            NativeArray<LocalTransform> transforms = resource.Query.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            int toRender = math.min(transforms.Length, m_InstanceMats.Length); 

            NativeArray<LocalTransformLast> transformsLast = default;
            
            if (resource.UseLastTransform)
            {
                transformsLast = resource.Query.ToComponentDataArray<LocalTransformLast>(Allocator.TempJob);
                AsyncRenderTransformGenerator asyncRenderTransformGenerator = new AsyncRenderTransformGenerator
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
                AsyncRenderTransformGeneratorLightweight asyncRenderTransformGenerator = new AsyncRenderTransformGeneratorLightweight
                {
                    transforms = transforms,
                    matrices = m_InstanceMats,
                };
                asyncRenderTransformGenerator.ScheduleParallel(toRender, 64, default).Complete();
            }
            
            NativeArray<SpriteAnimFrame> spriteIndices = default;
            NativeArray<float> spriteIndicesf = default;
            if (resource.Animated)
            {
                spriteIndices = resource.Query.ToComponentDataArray<SpriteAnimFrame>(Allocator.TempJob);
                spriteIndicesf = spriteIndices.Reinterpret<float>();
            }
            
            NativeArray<float> lifespan = default;
            if (resource.HasLifespan)
            {
                var destroyAtTime = resource.Query.ToComponentDataArray<DestroyAtTime>(Allocator.Temp).Reinterpret<double>();
                var spawnAtTime = resource.Query.ToComponentDataArray<SpawnTimeCreated>(Allocator.Temp).Reinterpret<double>();
                lifespan = new NativeArray<float>(destroyAtTime.Length, Allocator.Temp);
                for (int lifeIt = 0; lifeIt < destroyAtTime.Length; lifeIt++)
                    lifespan[lifeIt] = math.clamp((float)((SystemAPI.Time.ElapsedTime - spawnAtTime[lifeIt])/(destroyAtTime[lifeIt] - spawnAtTime[lifeIt])), 0, 1);
                destroyAtTime.Dispose();
                spawnAtTime.Dispose();
            }
            
            NativeArray<float> torusRads = default;
            if (resource.IsTorus)
            {
                torusRads = resource.Query.ToComponentDataArray<TorusMin>(Allocator.Temp).Reinterpret<float>();
            }
            
            NativeArray<float> torusAngles = default;
            if (resource.IsCone)
            {
                torusAngles = resource.Query.ToComponentDataArray<TorusCone>(Allocator.Temp).Reinterpret<float>();
            }
            
            var mesh = resource.Mesh;
            var renderParams = resource.RenderParams;
            
            for (int j = 0; j < toRender; j += Profiling.k_MaxInstances)
            {
                int count = math.min(Profiling.k_MaxInstances, toRender - j);
            
                if (resource.Animated)
                {
                    renderParams.matProps.SetFloatArray("spriteAnimFrameBuffer", new Span<float>(&((float*)spriteIndices.GetUnsafePtr())[j], count).ToArray());
                }
                if (resource.HasLifespan)
                {
                    renderParams.matProps.SetFloatArray("lifespanBuffer", new Span<float>(&((float*)lifespan.GetUnsafePtr())[j], count).ToArray());
                }
                if (resource.IsTorus)
                {
                    renderParams.matProps.SetFloatArray("torusMinBuffer", new Span<float>(&((float*)torusRads.GetUnsafePtr())[j], count).ToArray());
                }
                if (resource.IsCone)
                {
                    renderParams.matProps.SetFloatArray("torusAngleBuffer", new Span<float>(&((float*)torusAngles.GetUnsafePtr())[j], count).ToArray());
                }
                
                Graphics.RenderMeshInstanced(renderParams, mesh, 0, m_InstanceMats, count, j);
            }
            
            if (resource.ShowOnMap)
            {
                var mapRenderParams = resource.MapRenderParams;
                for (int j = 0; j < toRender; j += Profiling.k_MaxInstances)
                {
                    int count = math.min(Profiling.k_MaxInstances, toRender - j);
                
                    Graphics.RenderMeshInstanced(mapRenderParams, mesh, 0, m_InstanceMats, count, j);
                }
            }
            
            transforms.Dispose();
            
            if (transformsLast.IsCreated) transformsLast.Dispose();
            if (spriteIndices.IsCreated) spriteIndices.Dispose();
            if (lifespan.IsCreated) lifespan.Dispose();
            if (torusRads.IsCreated) torusRads.Dispose();
            if (torusAngles.IsCreated) torusAngles.Dispose();
        }
    }

    [BurstCompile]
    public partial struct AsyncRenderTransformGenerator : IJobFor
    {
        [ReadOnly] public float t;
        [ReadOnly] public NativeArray<LocalTransformLast> transformsLast;
        [ReadOnly] public NativeArray<LocalTransform> transforms;

        [WriteOnly] public NativeArray<Matrix4x4> matrices;

        [BurstCompile]
        public void Execute(int index)
        {
            var oldTransform = transformsLast[index];
            if (math.all(oldTransform.Value.Position == 0))
            {
                matrices[index] = transforms[index].ToMatrix();
                return;
            }
            
            var newTransform = transforms[index];
            
            var p = math.lerp(oldTransform.Value.Position, newTransform.Position, t);
            var q = math.slerp(oldTransform.Value.Rotation, newTransform.Rotation, t);
            matrices[index] = LocalTransform.FromPositionRotationScale(p, q, newTransform.Scale).ToMatrix();
        }
    }
    [BurstCompile]
    public partial struct AsyncRenderTransformGeneratorLightweight : IJobFor
    {
        [ReadOnly] public NativeArray<LocalTransform> transforms;
        [WriteOnly] public NativeArray<Matrix4x4> matrices;

        [BurstCompile]
        public void Execute(int index)
        {
            matrices[index] = transforms[index].ToMatrix();
        }
    }
}