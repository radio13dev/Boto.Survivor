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
                    link.SetLink(entity);

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
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct SpecificPrefabTrackSystem : ISystem
{
    EntityQuery m_query;

    public void OnCreate(ref SystemState state)
    {
        m_query = SystemAPI.QueryBuilder().WithAll<LocalTransform, SpecificPrefabProxy, SpecificPrefabRequest.DynamicTag>().Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        var transforms = m_query.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var proxies = m_query.ToComponentDataArray<SpecificPrefabProxy>(Allocator.Temp);
        var proxyTransforms = new Transform[proxies.Length];
        for (int i = 0; i < proxies.Length; i++)
            proxyTransforms[i] = proxies[i].Spawned.Value.transform;
        var transformAccessArray = new TransformAccessArray(proxyTransforms);

        // Initialize the job data
        var job = new ApplyLocalTransformToTransform()
        {
            transforms = transforms
        };

        // If this job required a previous job to complete before it could safely begin execution,
        // we'd use its handle here. For this simple case, there are no job dependencies,
        // so a default JobHandle is sufficient.
        JobHandle dependencyJobHandle = default;

        // Schedule a parallel-for-transform job.
        // The method takes a TransformAccessArray which contains the Transforms that will be acted on in the job.
        JobHandle jobHandle = job.ScheduleByRef(transformAccessArray, dependencyJobHandle);

        // Ensure the job has completed.
        // It is not recommended to Complete a job immediately,
        // since that reduces the chance of having other jobs run in parallel with this one.
        // You optimally want to schedule a job early in a frame and then wait for it later in the frame.
        // Ideally this job's JobHandle would be passed as a dependency to another job that consumes the
        // output of this one. If the output of this job must be read from the main thread, you should call
        // Complete() on this job handle just before reading it.
        jobHandle.Complete();

        // Native containers must be disposed manually.
        transformAccessArray.Dispose();
        proxies.Dispose();
        transforms.Dispose();
    }

    public struct ApplyLocalTransformToTransform : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<LocalTransform> transforms;

        public void Execute(int index, TransformAccess transform)
        {
            transform.SetPositionAndRotation(transforms[index].Position, transforms[index].Rotation);
        }
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
}