using BovineLabs.Core.SingletonCollection;
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
    
        public void Execute(in DynamicBuffer<ProjectileHitEntity> hits, in Projectile projectile)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                var e = networkIdMapping[hits[i].Value];
                if (HealthLookup.TryGetRefRW(e, out var otherEntityF))
                {
                    otherEntityF.ValueRW.Value -= projectile.Damage;
                    GameEvents.Trigger(GameEvents.Type.EnemyHealthChanged, e, -projectile.Damage);
                }
            }
        }
    }
}