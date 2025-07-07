using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Jobs;
using Object = UnityEngine.Object;

public class GenericPrefabAuthoring : MonoBehaviour
{
    public GameObject ToSpawn;
    public bool Dynamic;

    public class Baker : Baker<GenericPrefabAuthoring>
    {
        public override void Bake(GenericPrefabAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new GenericPrefabRequest(authoring.ToSpawn));
            if (authoring.Dynamic)
                AddComponent(entity, new GenericPrefabRequest.DynamicTag());
        }
    }
}

public struct GenericPrefabRequest : IComponentData
{
    public readonly UnityObjectRef<GameObject> ToSpawn;
    public readonly bool InWorldSpace;

    public GenericPrefabRequest(GameObject toSpawn, bool inWorldSpace = false)
    {
        ToSpawn = toSpawn;
        InWorldSpace = inWorldSpace;
    }

    public struct DynamicTag : IComponentData
    {
    }
}

public struct GenericPrefabProxy : ICleanupComponentData
{
    public UnityObjectRef<GameObject> Spawned;
}

public abstract class EntityLinkMono : MonoBehaviour
{
    Entity m_linkedEntity;

    public void SetLink(Entity entity)
    {
        m_linkedEntity = entity;
        OnSetLink();
    }

    public virtual void OnSetLink()
    {
    }

    public Entity GetLink()
    {
        return m_linkedEntity;
    }

    public bool HasLink() => m_linkedEntity != Entity.Null;
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class GenericPrefabSpawnSystem : SystemBase
{
    EntityQuery m_query;

    protected override void OnCreate()
    {
        base.OnCreate();
        m_query = SystemAPI.QueryBuilder().WithAll<GenericPrefabRequest>().WithNone<GenericPrefabProxy>().Build();
        RequireForUpdate(m_query);
    }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach ((var request, var transform, var entity) in SystemAPI.Query<RefRO<GenericPrefabRequest>, RefRO<LocalToWorld>>().WithNone<GenericPrefabProxy>().WithEntityAccess())
        {
            GameObject spawned = null;
            if (request.ValueRO.ToSpawn)
            {
                spawned = Object.Instantiate(request.ValueRO.ToSpawn.Value,
                    request.ValueRO.InWorldSpace ? float3.zero : transform.ValueRO.Position,
                    request.ValueRO.InWorldSpace ? quaternion.identity : transform.ValueRO.Rotation
                );
                foreach (var link in spawned.GetComponentsInChildren<EntityLinkMono>(true))
                    link.SetLink(entity);
            }
            else
            {
                Debug.LogError($"Failed to spawn {request.ValueRO.ToSpawn} from {entity}, the prefab is null or not set.");
            }

            ecb.AddComponent(entity, new GenericPrefabProxy()
            {
                Spawned = spawned
            });
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}

[RequireMatchingQueriesForUpdate]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial struct GenericPrefabTrackSystem : ISystem
{
    EntityQuery m_query;
    
    public void OnCreate(ref SystemState state)
    {
        m_query = SystemAPI.QueryBuilder().WithAll<LocalTransform, GenericPrefabProxy, GenericPrefabRequest.DynamicTag>().Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        var transforms = m_query.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var proxies = m_query.ToComponentDataArray<GenericPrefabProxy>(Allocator.Temp);
        var proxyTransforms = new Transform[proxies.Length];
        for (int i = 0; i < proxies.Length; i++)
            proxyTransforms[i] = proxies[i].Spawned.Value.transform;
        var transformAccessArray = new TransformAccessArray(proxyTransforms);

        // Initialize the job data
        var job = new ApplyVelocityJobParallelForTransform()
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

    public struct ApplyVelocityJobParallelForTransform : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<LocalTransform> transforms;

        public void Execute(int index, TransformAccess transform)
        {
            transform.SetPositionAndRotation(transforms[index].Position, transforms[index].Rotation);
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class GenericPrefabCleanupSystem : SystemBase
{
    EntityQuery m_query;

    protected override void OnCreate()
    {
        base.OnCreate();
        m_query = SystemAPI.QueryBuilder().WithAll<GenericPrefabProxy>().WithNone<GenericPrefabRequest>().Build();
        RequireForUpdate(m_query);
    }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach ((var proxy, var entity) in SystemAPI.Query<RefRO<GenericPrefabProxy>>().WithNone<GenericPrefabRequest>().WithEntityAccess())
        {
            if (proxy.ValueRO.Spawned) Object.Destroy(proxy.ValueRO.Spawned.Value.gameObject);
            ecb.RemoveComponent<GenericPrefabProxy>(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}