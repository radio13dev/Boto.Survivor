using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(ProjectileSystemGroup))]
[BurstCompile]
public partial struct ProjectileHitSystem_Push : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var forceLookup = SystemAPI.GetComponentLookup<Force>(false);
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        foreach (var (hit, movement, transform) in SystemAPI.Query<RefRO<ProjectileHit>, RefRO<SurfaceMovement>, RefRO<LocalTransform>>())
        {
            if (!forceLookup.TryGetRefRW(hit.ValueRO.HitEntity, out var hitEntityF))
                continue;
            
            var hitEntityT = transformLookup[hit.ValueRO.HitEntity];
            hitEntityF.ValueRW.Shift += hitEntityT.TransformDirection(movement.ValueRO.Velocity.f3z()/20);
        }
    }
}