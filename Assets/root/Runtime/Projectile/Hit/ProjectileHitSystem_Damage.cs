using Unity.Burst;
using Unity.Entities;

[UpdateInGroup(typeof(ProjectileSystemGroup))]
[UpdateBefore(typeof(ProjectileClearSystem))]
[BurstCompile]
public partial struct ProjectileHitSystem_Damage : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var healthLookup = SystemAPI.GetComponentLookup<Health>(false);
        foreach (var hit in SystemAPI.Query<RefRO<ProjectileHit>>())
        {
            if (!healthLookup.TryGetRefRW(hit.ValueRO.HitEntity, out var hitEntityH))
                continue;
            
            hitEntityH.ValueRW.Value--;
        }
    }
}