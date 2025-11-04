using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class EnemySpawnerFlagZoneAuthoring : MonoBehaviour
{
    public EnemySpawnerFlagZone Zone;

    partial class Baker : Baker<EnemySpawnerFlagZoneAuthoring>
    {
        public override void Bake(EnemySpawnerFlagZoneAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, authoring.Zone);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Zone.Mode.GetColor();
        Gizmos.DrawWireSphere(transform.position, MathF.Sqrt(Zone.RadiusSqr));
    }
}


[Serializable]
public struct EnemySpawnerFlagZone : IComponentData
{
    public float RadiusSqr;
    public EnemySpawnerMode Mode;
}

public partial struct EnemySpawnerFlagZoneSystem : ISystem
{
    EntityQuery m_ZoneQuery;

    public void OnCreate(ref SystemState state)
    {
        m_ZoneQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, EnemySpawnerFlagZone>().Build();
        state.RequireForUpdate(m_ZoneQuery);
        state.RequireForUpdate<EnemySpawner>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var zoneTransforms = m_ZoneQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var zoneFlags = m_ZoneQuery.ToComponentDataArray<EnemySpawnerFlagZone>(Allocator.TempJob);
        state.Dependency = new Job()
        {
            ZoneTransforms = zoneTransforms,
            ZoneFlags = zoneFlags
        }.Schedule(state.Dependency);
        zoneTransforms.Dispose(state.Dependency);
        zoneFlags.Dispose(state.Dependency);
    }
    
    [WithPresent(typeof(EnemySpawner))]
    partial struct Job : IJobEntity
    {
        [ReadOnly] public NativeArray<LocalTransform> ZoneTransforms;
        [ReadOnly] public NativeArray<EnemySpawnerFlagZone> ZoneFlags;
    
        public void Execute(in LocalTransform transform, ref EnemySpawner spawner, EnabledRefRW<EnemySpawner> spawnerEnabled)
        {
            spawner.Mode = EnemySpawnerMode.Wave_01_Common;
            
            bool wasInside = false;
            int insideI = -1;
            float insideD = float.MaxValue;
            LocalTransform insideT = default;
            for (int i = 0; i < ZoneTransforms.Length; i++)
            {
                var d = math.distancesq(transform.Position, ZoneTransforms[i].Position);
                if (d < ZoneFlags[i].RadiusSqr)
                {
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
            }
            
            spawnerEnabled.ValueRW = spawner.Mode != EnemySpawnerMode.None;
        }
    }
}