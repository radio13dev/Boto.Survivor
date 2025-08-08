using Unity.Burst;
using Unity.Entities;

[UpdateInGroup(typeof(ProjectileSystemGroup))]
[BurstCompile]
public partial struct ProjectileHitSystem_Damage : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new Job()
        {
            HealthLookup = SystemAPI.GetComponentLookup<Health>(false)
        }.Schedule(state.Dependency);
    }
    
    [WithAll(typeof(ProjectileHit))]
    partial struct Job : IJobEntity
    {
        public ComponentLookup<Health> HealthLookup;
    
        public void Execute(in DynamicBuffer<ProjectileHitEntity> hits)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                if (HealthLookup.TryGetRefRW(hits[i].Value, out var otherEntityF))
                {
                    otherEntityF.ValueRW.Value--;
                }
            }
        }
    }
}