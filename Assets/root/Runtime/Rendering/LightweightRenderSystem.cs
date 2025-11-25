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
    bool m_Init => m_InstanceQueries.IsCreated;
    NativeArray<Matrix4x4> m_InstanceMats;
    public NativeArray<EntityQuery> m_InstanceQueries;

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
            m_InstanceQueries = new NativeArray<EntityQuery>(resources.Length, Allocator.Persistent);
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
                
                if (resourceData.IsColorBaseColor) queryBuilder = queryBuilder.WithAll<ColorBaseColor>();
                if (resourceData.IsColorA) queryBuilder = queryBuilder.WithAll<ColorA>();
                if (resourceData.IsColorB) queryBuilder = queryBuilder.WithAll<ColorB>();
                if (resourceData.IsColorC) queryBuilder = queryBuilder.WithAll<ColorC>();
                
                m_InstanceQueries[resourceIt] = queryBuilder.Build(ref state);
            }
        }

        for (int resourceIt = 0; resourceIt < resources.Length; resourceIt++)
        {
            if (!resources[resourceIt].Valid) continue;
            
            var resource = resources[resourceIt].Instance.Value;
            var query = m_InstanceQueries[resourceIt];
            query.SetSharedComponentFilter(new InstancedResourceRequest(resourceIt));
            if (query.IsEmpty)
                continue;
            
            NativeArray<LocalTransform> transforms = query.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            int toRender = math.min(transforms.Length, m_InstanceMats.Length); 

            NativeArray<LocalTransformLast> transformsLast = default;
            
            if (resource.UseLastTransform)
            {
                transformsLast = query.ToComponentDataArray<LocalTransformLast>(Allocator.TempJob);
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
                spriteIndices = query.ToComponentDataArray<SpriteAnimFrame>(Allocator.TempJob);
                spriteIndicesf = spriteIndices.Reinterpret<float>();
            }
            
            NativeArray<float> lifespan = default;
            if (resource.HasLifespan)
            {
                var destroyAtTime = query.ToComponentDataArray<DestroyAtTime>(Allocator.Temp).Reinterpret<double>();
                var spawnAtTime = query.ToComponentDataArray<SpawnTimeCreated>(Allocator.Temp).Reinterpret<double>();
                lifespan = new NativeArray<float>(destroyAtTime.Length, Allocator.Temp);
                for (int lifeIt = 0; lifeIt < destroyAtTime.Length; lifeIt++)
                    lifespan[lifeIt] = math.clamp((float)((SystemAPI.Time.ElapsedTime - spawnAtTime[lifeIt])/(destroyAtTime[lifeIt] - spawnAtTime[lifeIt])), 0, 1);
                destroyAtTime.Dispose();
                spawnAtTime.Dispose();
            }
            
            NativeArray<float> torusRads = default;
            if (resource.IsTorus)
            {
                torusRads = query.ToComponentDataArray<TorusMin>(Allocator.Temp).Reinterpret<float>();
            }
            
            NativeArray<float> torusAngles = default;
            if (resource.IsCone)
            {
                torusAngles = query.ToComponentDataArray<TorusCone>(Allocator.Temp).Reinterpret<float>();
            }
            
            NativeArray<float4> colorBaseColor = default;
            NativeArray<float4> colorA = default;
            NativeArray<float4> colorB = default;
            NativeArray<float4> colorC = default;
            if (resource.IsColorBaseColor) colorBaseColor = query.ToComponentDataArray<ColorBaseColor>(Allocator.Temp).Reinterpret<float4>();
            if (resource.IsColorA) colorA = query.ToComponentDataArray<ColorA>(Allocator.Temp).Reinterpret<float4>();
            if (resource.IsColorB) colorB = query.ToComponentDataArray<ColorB>(Allocator.Temp).Reinterpret<float4>();
            if (resource.IsColorC) colorC = query.ToComponentDataArray<ColorC>(Allocator.Temp).Reinterpret<float4>();
            
            var instanceMats = m_InstanceMats;
            var mesh = resource.Mesh;
            DoRender(resource.RenderParams);
            
            if (resource.ShowOnMap)
                DoRender(resource.MapRenderParams);
            
            void DoRender(RenderParams renderParams)
            {
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
                    if (resource.IsColorBaseColor)
                        renderParams.matProps.SetVectorArray("colorBaseColorBuffer", new Span<Vector4>(&((Vector4*)colorBaseColor.GetUnsafePtr())[j], count).ToArray());
                    if (resource.IsColorA)
                        renderParams.matProps.SetVectorArray("colorABuffer", new Span<Vector4>(&((Vector4*)colorA.GetUnsafePtr())[j], count).ToArray());
                    if (resource.IsColorB)
                        renderParams.matProps.SetVectorArray("colorBBuffer", new Span<Vector4>(&((Vector4*)colorB.GetUnsafePtr())[j], count).ToArray());
                    if (resource.IsColorC)
                        renderParams.matProps.SetVectorArray("colorCBuffer", new Span<Vector4>(&((Vector4*)colorC.GetUnsafePtr())[j], count).ToArray());
                    Graphics.RenderMeshInstanced(renderParams, mesh, 0, instanceMats, count, j);
                }
            }
            
            transforms.Dispose();
            
            if (transformsLast.IsCreated) transformsLast.Dispose();
            if (spriteIndices.IsCreated) spriteIndices.Dispose();
            if (lifespan.IsCreated) lifespan.Dispose();
            if (torusRads.IsCreated) torusRads.Dispose();
            if (torusAngles.IsCreated) torusAngles.Dispose();
            if (colorBaseColor.IsCreated) colorBaseColor.Dispose();
            if (colorA.IsCreated) colorA.Dispose();
            if (colorB.IsCreated) colorB.Dispose();
            if (colorC.IsCreated) colorC.Dispose();
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