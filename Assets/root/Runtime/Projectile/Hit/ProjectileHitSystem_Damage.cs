using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

[UpdateInGroup(typeof(ProjectileSystemGroup))]
[BurstCompile]
public partial struct ProjectileHitSystem_Damage : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkIdMapping>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new Job()
        {
            HealthLookup = SystemAPI.GetComponentLookup<Health>(false),
            networkIdMapping = SystemAPI.GetSingleton<NetworkIdMapping>()
        }.Schedule(state.Dependency);
    }
    
    [WithAll(typeof(ProjectileHit))]
    partial struct Job : IJobEntity
    {
        public ComponentLookup<Health> HealthLookup;
        [ReadOnly] public NetworkIdMapping networkIdMapping;
    
        public void Execute(in DynamicBuffer<ProjectileHitEntity> hits)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                if (HealthLookup.TryGetRefRW(networkIdMapping[hits[i].Value], out var otherEntityF))
                {
                    otherEntityF.ValueRW.Value--;
                }
            }
        }
    }
}