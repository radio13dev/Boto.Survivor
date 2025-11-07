using System;
using Drawing;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Collider = Collisions.Collider;

public class EnemySpawnerFlagZoneAuthoring : MonoBehaviourGizmos
{
    public EnemySpawnerFlagZone Zone;

    partial class Baker : Baker<EnemySpawnerFlagZoneAuthoring>
    {
        public override void Bake(EnemySpawnerFlagZoneAuthoring authoring)
        {
            if (!DependsOn(GetComponent<ColliderAuthoring>()))
            {
                Debug.LogError($"EnemySpawnerFlagZoneAuthoring must have a ColliderAuthoring component on the same GameObject: {authoring.gameObject}", authoring);
                return;
            }
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, authoring.Zone);
            AddComponent(entity, new SpawnAnimation(authoring.transform.lossyScale.x));
            SetComponentEnabled<SpawnAnimation>(entity, true);
        }
    }

    public override void DrawGizmos()
    {
        if (!TryGetComponent<ColliderAuthoring>(out var cAuthoring)) return;
        var draw = Draw.editor;
        var c = cAuthoring.Collider.Apply(LocalTransform.FromPositionRotationScale(transform.position, transform.rotation, transform.lossyScale.x*1.05f));
        c.DebugDraw(draw, Color.red);
    }
}


[Serializable]
public struct EnemySpawnerFlagZone : IComponentData
{
    public EnemySpawnerMode Mode;
}

public partial struct EnemySpawnerFlagZoneSystem : ISystem
{
    EntityQuery m_ZoneQuery;

    public void OnCreate(ref SystemState state)
    {
        m_ZoneQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, EnemySpawnerFlagZone, Collider>().Build();
        state.RequireForUpdate(m_ZoneQuery);
        state.RequireForUpdate<EnemySpawner>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var zoneTransforms = m_ZoneQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var zoneFlags = m_ZoneQuery.ToComponentDataArray<EnemySpawnerFlagZone>(Allocator.TempJob);
        var zoneColliders = m_ZoneQuery.ToComponentDataArray<Collider>(Allocator.TempJob);
        state.Dependency = new Job()
        {
            ZoneTransforms = zoneTransforms,
            ZoneFlags = zoneFlags,
            ZoneColliders = zoneColliders
        }.Schedule(state.Dependency);
        zoneTransforms.Dispose(state.Dependency);
        zoneFlags.Dispose(state.Dependency);
        zoneColliders.Dispose(state.Dependency);
    }
    
    [WithPresent(typeof(EnemySpawner))]
    partial struct Job : IJobEntity
    {
        [ReadOnly] public NativeArray<LocalTransform> ZoneTransforms;
        [ReadOnly] public NativeArray<EnemySpawnerFlagZone> ZoneFlags;
        [ReadOnly] public NativeArray<Collider> ZoneColliders;
    
        public void Execute(in LocalTransform transform, ref EnemySpawner spawner, EnabledRefRW<EnemySpawner> spawnerEnabled, in Collider collider)
        {
            spawner.Mode = EnemySpawnerMode.Wave_01_Common;
            
            bool wasInside = false;
            int insideI = -1;
            float insideD = float.MaxValue;
            LocalTransform insideT = default;
            var myC = collider.Apply(transform);
            for (int i = 0; i < ZoneTransforms.Length; i++)
            {
                if (myC.Overlaps(ZoneColliders[i].Apply(ZoneTransforms[i])) == false)
                    continue;
                var d = math.distancesq(transform.Position, ZoneTransforms[i].Position);
                if (wasInside)
                {
                    if (d > insideD) continue;
                    int c = new LocalTransformComparer(ZoneTransforms).Compare(i, insideI);
                    if (c == 0)
                    {
                        Debug.LogError($"Same distance from two spawners: {ZoneTransforms[i]} and {insideT}");
                        continue;
                    }
                    if (c == 1)
                    {
                        continue;
                    }
                }
                    
                spawner.Mode = ZoneFlags[i].Mode;
                wasInside = true;
                insideI = i;
                insideD = d;
                insideT = ZoneTransforms[i];
            }
            
            spawnerEnabled.ValueRW = spawner.Mode != EnemySpawnerMode.None;
        }
    }
}