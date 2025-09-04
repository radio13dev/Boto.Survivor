using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Jobs;
using Object = UnityEngine.Object;

public struct SpecificPrefabRequest : IComponentData
{
    public readonly int ToSpawn;
    public readonly bool InWorldSpace;

    public SpecificPrefabRequest(int toSpawn, bool inWorldSpace = false)
    {
        ToSpawn = toSpawn;
        InWorldSpace = inWorldSpace;
    }

    public struct DynamicTag : IComponentData
    {
    }
}

public struct SpecificPrefabProxy : ICleanupComponentData
{
    public UnityObjectRef<GameObject> Spawned;
}

[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
public partial class SpecificPrefabSpawnSystem : SystemBase
{
    public Game GameReference;
    EntityQuery m_query;

    protected override void OnCreate()
    {
        base.OnCreate();
        m_query = SystemAPI.QueryBuilder().WithAll<SpecificPrefabRequest>().WithNone<SpecificPrefabProxy>().Build();
        RequireForUpdate<GameManager.SpecificPrefabs>();
        RequireForUpdate(m_query);
    }

    protected override void OnUpdate()
    {
        var resources = SystemAPI.GetSingletonBuffer<GameManager.SpecificPrefabs>();
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach ((var request, var transform, var entity) in
                 SystemAPI.Query<RefRO<SpecificPrefabRequest>, RefRO<LocalToWorld>>().WithNone<SpecificPrefabProxy>().WithEntityAccess())
        {
            GameObject spawned = null;
            if (request.ValueRO.ToSpawn >= 0 && request.ValueRO.ToSpawn < resources.Length)
            {
                var prefab = resources[request.ValueRO.ToSpawn].Prefab;
                spawned = Object.Instantiate(prefab.Value,
                    request.ValueRO.InWorldSpace ? float3.zero : transform.ValueRO.Position,
                    request.ValueRO.InWorldSpace ? quaternion.identity : transform.ValueRO.Rotation
                );
                foreach (var link in spawned.GetComponentsInChildren<EntityLinkMono>(true))
                    link.SetLink(GameReference, entity);

                ecb.AddComponent(entity, new SpecificPrefabProxy()
                {
                    Spawned = spawned
                });
            }
            else
            {
                Debug.LogWarning($"Failed to spawn {request.ValueRO.ToSpawn} from {entity}, the prefab is null or not set.");
                ecb.RemoveComponent<SpecificPrefabRequest>(entity);
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}

[RequireMatchingQueriesForUpdate]
[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
[UpdateInGroup(typeof(RenderSystemGroup))]
public partial struct SpecificPrefabTrackSystem : ISystem
{
    EntityQuery m_query;
    TransformAccessArray m_AccessArray;
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<RenderSystemHalfTime>();
        m_query = SystemAPI.QueryBuilder().WithAll<LocalTransform, LocalTransformLast, SpecificPrefabProxy, SpecificPrefabRequest.DynamicTag>().Build();
    }

    public void OnDestroy(ref SystemState state)
    {
        if (m_AccessArray.isCreated) m_AccessArray.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        var transforms = m_query.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var transformsLast = m_query.ToComponentDataArray<LocalTransformLast>(Allocator.TempJob);
        var proxies = m_query.ToComponentDataArray<SpecificPrefabProxy>(Allocator.TempJob);
        var proxyTransforms = new Transform[proxies.Length];
        for (int i = 0; i < proxies.Length; i++)
            proxyTransforms[i] = proxies[i].Spawned.Value.transform;
            
        if (m_AccessArray.isCreated) m_AccessArray.Dispose();
        m_AccessArray = new TransformAccessArray(proxyTransforms);

        // Initialize the job data
        var job = new GenericPrefabTrackSystem.ApplyLocalTransformToTransform()
        {
            T = SystemAPI.GetSingleton<RenderSystemHalfTime>().Value,
            transforms = transforms,
            transformsLast = transformsLast
        };

        // Schedule a parallel-for-transform job.
        // The method takes a TransformAccessArray which contains the Transforms that will be acted on in the job.
        state.Dependency = job.ScheduleByRef(m_AccessArray, state.Dependency);
        proxies.Dispose(state.Dependency);
        transforms.Dispose(state.Dependency);
        transformsLast.Dispose(state.Dependency);
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
public partial class SpecificPrefabCleanupSystem : SystemBase
{
    EntityQuery m_query;

    protected override void OnCreate()
    {
        base.OnCreate();
        m_query = SystemAPI.QueryBuilder().WithAll<SpecificPrefabProxy>().WithNone<SpecificPrefabRequest>().Build();
        RequireForUpdate(m_query);
    }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach ((var proxy, var entity) in SystemAPI.Query<RefRO<SpecificPrefabProxy>>().WithNone<SpecificPrefabRequest>().WithEntityAccess())
        {
            if (proxy.ValueRO.Spawned) Object.Destroy(proxy.ValueRO.Spawned.Value.gameObject);
            ecb.RemoveComponent<SpecificPrefabProxy>(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        using var components = m_query.ToComponentDataArray<SpecificPrefabProxy>(Allocator.Temp);
        for (int i = 0; i < components.Length; i++)
            if (components[i].Spawned) Object.Destroy(components[i].Spawned.Value.gameObject);
    }
}