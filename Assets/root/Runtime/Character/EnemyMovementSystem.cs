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
        m_TargetQuery = SystemAPI.QueryBuilder().WithAll<PlayerControlled, LocalTransform2D>().Build();
        state.RequireForUpdate(m_TargetQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        var targets = m_TargetQuery.ToComponentDataArray<LocalTransform2D>(Allocator.TempJob);
        new Job()
        {
            PlayerTransform = targets[0]
        }.Schedule();
        targets.Dispose();
    }
    
    [WithAll(typeof(EnemyTag))]
    partial struct Job : IJobEntity
    {
        [ReadOnly] public LocalTransform2D PlayerTransform;
        public void Execute(in LocalTransform2D localTransform, in Movement movement, ref StepInput input)
        {
            input = new StepInput(){Direction = PlayerTransform.Position - localTransform.Position };
        }
    }
}