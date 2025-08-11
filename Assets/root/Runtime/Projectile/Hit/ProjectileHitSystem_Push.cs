using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(ProjectileSystemGroup))]
[BurstCompile]
public partial struct ProjectileHitSystem_Push : ISystem
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
            ForceLookup = SystemAPI.GetComponentLookup<Force>(false),
            networkIdMapping = SystemAPI.GetSingleton<NetworkIdMapping>()
        }.Schedule(state.Dependency);
    }
    
    [WithAll(typeof(ProjectileHit))]
    partial struct Job : IJobEntity
    {
        public ComponentLookup<Force> ForceLookup;
        [ReadOnly] public NetworkIdMapping networkIdMapping;
    
        public void Execute(in SurfaceMovement movement, in LocalTransform hitT, in DynamicBuffer<ProjectileHitEntity> hits)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                if (ForceLookup.TryGetRefRW(networkIdMapping[hits[i].Value], out var otherEntityF))
                {
                    otherEntityF.ValueRW.Velocity += hitT.TransformDirection(movement.Velocity.f3z() * 3);
                }
            }
        }
    }
}