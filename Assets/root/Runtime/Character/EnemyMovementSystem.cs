using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[RequireMatchingQueriesForUpdate]
public partial struct EnemyMovementSystem : ISystem
{
    EntityQuery m_TargetQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemyTag>();
        m_TargetQuery = SystemAPI.QueryBuilder().WithAll<PlayerControlled, LocalTransform>().Build();
        state.RequireForUpdate(m_TargetQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        var targets = m_TargetQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        TorusMapper.CartesianToToroidal(targets[0].Position, out var x, out var y, out _);
        float2 targetToroidal = new float2(x,y);
         
        new Job()
        {
            TargetToroidal = targetToroidal
        }.Schedule();
        targets.Dispose();
    }
    
    [WithAll(typeof(EnemyTag))]
    partial struct Job : IJobEntity
    {
        [ReadOnly] public float2 TargetToroidal;
        public void Execute(in LocalTransform localTransform, in Movement movement, ref StepInput input)
        {
            // Convert our position and the targets position into cartesian/torodial coordinates
            TorusMapper.CartesianToToroidal(localTransform.Position, out var x, out var y, out var ringCenterOffset);
            var myToroidal = new float2(x,y);
            var dirToroidal = TargetToroidal - myToroidal;
            var dir = TorusMapper.ToroidalToCartesian(dirToroidal.x, dirToroidal.y);
            
            dir = localTransform.InverseTransformDirection(dir); // Convert the direction into a direction relative to our forward and right vectors (hope this works)
            input = new StepInput(){Direction = dir.xz };
        }
    }
}