using System;
using System.Collections;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Jobs;
using math = Unity.Mathematics.math;
using Object = UnityEngine.Object;

public class GenericPrefabAuthoring : MonoBehaviour
{
    public GameObject ToSpawn;
    public bool Dynamic;
    public bool InWorldSpace;

    public class Baker : Baker<GenericPrefabAuthoring>
    {
        public override void Bake(GenericPrefabAuthoring authoring)
        {
            if (!DependsOn(authoring.ToSpawn))
            {
                Debug.LogError($"Invalid prefab on {authoring}: {authoring.ToSpawn}");
                return;
            }
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new GenericPrefabRequest(authoring.ToSpawn, authoring.InWorldSpace));
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
    public Game Game => m_Game;
    Game m_Game;
    
    public Entity Entity => m_linkedEntity;
    Entity m_linkedEntity;

    public void SetLink(Game gameReference, Entity entity)
    {
        m_Game = gameReference;
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

#if UNITY_EDITOR
    public virtual IEnumerator Start()
    {
        yield return null;
        if (Game == null)
        {
            enabled = false;
            throw new Exception($"Game is null on {this} {gameObject.name}.");
        }
    }
#endif
}

[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
public partial class GenericPrefabSpawnSystem : SystemBase
{
    public Game GameReference;
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
        foreach ((var request, var transform, var entity) in SystemAPI.Query<RefRO<GenericPrefabRequest>, RefRO<LocalTransform>>().WithNone<GenericPrefabProxy, Hidden>().WithEntityAccess())
        {
            GameObject spawned = null;
            if (request.ValueRO.ToSpawn)
            {
                spawned = Object.Instantiate(request.ValueRO.ToSpawn.Value,
                    request.ValueRO.InWorldSpace ? float3.zero : transform.ValueRO.Position,
                    request.ValueRO.InWorldSpace ? quaternion.identity : transform.ValueRO.Rotation
                );
                Debug.Log($"Spawned generic prefab: {spawned}");
                spawned.transform.localScale = Vector3.one*transform.ValueRO.Scale;
                foreach (var link in spawned.GetComponentsInChildren<EntityLinkMono>(true))
                    link.SetLink(GameReference, entity);

                ecb.AddComponent(entity, new GenericPrefabProxy()
                {
                    Spawned = spawned
                });
            }
            else
            {
                Debug.LogWarning($"Failed to spawn {request.ValueRO.ToSpawn} from {entity}, the prefab is null or not set.");
                ecb.RemoveComponent<GenericPrefabRequest>(entity);
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}

[RequireMatchingQueriesForUpdate]
[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
[UpdateInGroup(typeof(RenderSystemGroup))]
public partial struct GenericPrefabTrackSystem : ISystem
{
    EntityQuery m_query;
    TransformAccessArray m_AccessArray;
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<RenderSystemHalfTime>();
        m_query = SystemAPI.QueryBuilder().WithAll<LocalTransform, LocalTransformLast, GenericPrefabProxy, GenericPrefabRequest.DynamicTag>().Build();
    }

    public void OnDestroy(ref SystemState state)
    {
        if (m_AccessArray.isCreated) m_AccessArray.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        var transforms = m_query.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var transformsLast = m_query.ToComponentDataArray<LocalTransformLast>(Allocator.TempJob);
        var proxies = m_query.ToComponentDataArray<GenericPrefabProxy>(Allocator.TempJob);
        var proxyTransforms = new Transform[proxies.Length];
        for (int i = 0; i < proxies.Length; i++)
            proxyTransforms[i] = proxies[i].Spawned.Value.transform;
            
        if (m_AccessArray.isCreated) m_AccessArray.Dispose();
        m_AccessArray = new TransformAccessArray(proxyTransforms);

        // Initialize the job data
        var job = new ApplyLocalTransformToTransform()
        {
            dt = 4.0f/60.0f,
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

    [BurstCompile]
    public struct ApplyLocalTransformToTransform : IJobParallelForTransform
    {
        [ReadOnly] public float dt;
        [ReadOnly] public float T;
        [ReadOnly] public NativeArray<LocalTransform> transforms;
        [ReadOnly] public NativeArray<LocalTransformLast> transformsLast;

        [BurstCompile]
        public void Execute(int index, TransformAccess transform)
        {
            // Lerp to new pos
            var finalPos = math.lerp(transformsLast[index].Value.Position, transforms[index].Position, T);
    
            // The 'up' direction should adjust instantly, but the forward direction should be lerped over time.
            var newRot = math.slerp(transformsLast[index].Value.Rotation, transforms[index].Rotation, T);
            var newUp = math.mul(newRot, math.up()); // Use this for all calculations
        
            var oldForward = math.mul(transform.rotation, math.forward());
            oldForward = math.cross(math.cross(newUp, oldForward), newUp); // Old forward to lerp from
        
            var newForward = math.mul(newRot, math.forward());
            newForward = math.cross(math.cross(newUp, newForward), newUp); // New forward to lerp to
        
            var finalForward = math.lerp(oldForward, newForward, dt);
            var finalRot = quaternion.LookRotationSafe(finalForward, newUp);
        
            var scale = math.lerp(transformsLast[index].Value.Scale, transforms[index].Scale, T);
            
            transform.SetPositionAndRotation(finalPos, finalRot);
            transform.localScale = new Vector3(scale, scale, scale);
        }
    }
}

[RequireMatchingQueriesForUpdate]
[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
[UpdateInGroup(typeof(RenderSystemGroup))]
public partial struct GenericPrefabTrackSystem_Light : ISystem
{
    EntityQuery m_query;
    TransformAccessArray m_AccessArray;
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<RenderSystemHalfTime>();
        m_query = SystemAPI.QueryBuilder().WithAll<LocalTransform, GenericPrefabProxy, GenericPrefabRequest.DynamicTag>().WithNone<LocalTransformLast>().Build();
    }

    public void OnDestroy(ref SystemState state)
    {
        if (m_AccessArray.isCreated) m_AccessArray.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        var transforms = m_query.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var proxies = m_query.ToComponentDataArray<GenericPrefabProxy>(Allocator.TempJob);
        var proxyTransforms = new Transform[proxies.Length];
        for (int i = 0; i < proxies.Length; i++)
            proxyTransforms[i] = proxies[i].Spawned.Value.transform;
            
        if (m_AccessArray.isCreated) m_AccessArray.Dispose();
        m_AccessArray = new TransformAccessArray(proxyTransforms);

        // Initialize the job data
        var job = new ApplyLocalTransformToTransform()
        {
            transforms = transforms,
        };

        // Schedule a parallel-for-transform job.
        // The method takes a TransformAccessArray which contains the Transforms that will be acted on in the job.
        state.Dependency = job.ScheduleByRef(m_AccessArray, state.Dependency);
        proxies.Dispose(state.Dependency);
        transforms.Dispose(state.Dependency);
    }

    [BurstCompile]
    public struct ApplyLocalTransformToTransform : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<LocalTransform> transforms;

        [BurstCompile]
        public void Execute(int index, TransformAccess transform)
        {
            transform.SetPositionAndRotation(transforms[index].Position, transforms[index].Rotation);
            var scale = transforms[index].Scale;
            transform.localScale = new Vector3(scale, scale, scale);
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
[UpdateAfter(typeof(DestroySystemGroup))]
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
            if (proxy.ValueRO.Spawned)
            {
                Debug.Log($"Cleaning up {proxy.ValueRO.Spawned.Value}...");
                if (proxy.ValueRO.Spawned.Value.TryGetComponent<DelayedCleanupEntityLinkMono>(out var cleanup))
                    cleanup.StartDestroy();
                else 
                    Object.Destroy(proxy.ValueRO.Spawned.Value.gameObject);
            }
            ecb.RemoveComponent<GenericPrefabProxy>(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        using var components = SystemAPI.QueryBuilder().WithAll<GenericPrefabProxy>().Build().ToComponentDataArray<GenericPrefabProxy>(Allocator.Temp);
        Debug.Log($"Cleaning up {components.Length} destroyed GenericPrefabs");
        for (int i = 0; i < components.Length; i++)
            if (components[i].Spawned) Object.Destroy(components[i].Spawned.Value.gameObject);
    }
}