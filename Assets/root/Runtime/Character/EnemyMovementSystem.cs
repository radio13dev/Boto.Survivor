using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = UnityEngine.Random;

[RequireMatchingQueriesForUpdate]
[BurstCompile]
[UpdateInGroup(typeof(MovementSystemGroup))]
public partial struct EnemyMovementSystem : ISystem
{
    EntityQuery m_TargetQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyTag>();
        m_TargetQuery = SystemAPI.QueryBuilder().WithAll<PlayerControlled, LocalTransform>().Build();
        state.RequireForUpdate(m_TargetQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var targets = m_TargetQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        state.Dependency = new Job()
        {
            Targets = targets,
        }.Schedule(state.Dependency);
        targets.Dispose(state.Dependency);
    }
    
    [BurstCompile]
    [WithAll(typeof(EnemyTag))]
    partial struct Job : IJobEntity
    {   
        [ReadOnly] public NativeArray<LocalTransform> Targets;
        public void Execute(in LocalTransform localTransform, in Movement movement, ref StepInput input)
        {
            float3 dir = float3.zero;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Targets.Length; i++)
            {
                var dif = math.abs(localTransform.Position - Targets[i].Position);
                var dist = dif.x + dif.y + dif.z;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    dir = Targets[i].Position - localTransform.Position;
                }
            }
            var movDir = localTransform.InverseTransformDirection(dir); // Convert the direction into a direction relative to our forward and right vectors (hope this works)
            input = new StepInput(){Direction = math.normalizesafe(dir.xz) };
        }
    }
}