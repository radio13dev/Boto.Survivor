using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[RequireMatchingQueriesForUpdate]
[BurstCompile]
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
        new Job()
        {
            Target = targets[0].Position,
            TargetToroidal = TorusMapper.CartesianToToroidal(targets[0].Position)
        }.Schedule();
        targets.Dispose();
    }
    
    [BurstCompile]
    [WithAll(typeof(EnemyTag))]
    partial struct Job : IJobEntity
    {   
        [ReadOnly] public float3 Target;
        [ReadOnly] public float2 TargetToroidal;
        public void Execute(in LocalTransform localTransform, in Movement movement, ref StepInput input)
        {
            var dir = localTransform.InverseTransformDirection(Target - localTransform.Position); // Convert the direction into a direction relative to our forward and right vectors (hope this works)
            input = new StepInput(){Direction = math.normalizesafe(dir.xz) };
        }
    }
}