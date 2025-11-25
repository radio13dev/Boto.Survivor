using BovineLabs.Saving;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[Save]
public struct ParticleOnDamage : IComponentData
{
    public int ParticleIndex;
}

public class ParticleOnDamageAuthoring : MonoBehaviour
{
    public DatabaseRef<PooledParticle, ParticleDatabase> Particle = new();

    public class Baker : Baker<ParticleOnDamageAuthoring>
    {
        public override void Bake(ParticleOnDamageAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new ParticleOnDamage(){ ParticleIndex = authoring.Particle.GetAssetIndex() });
        }
    }
}

[UpdateInGroup(typeof(ProjectileDamageSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
public partial struct ProjectileHitSystem_Particle : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkIdMapping>();
        state.RequireForUpdate<GameManager.Particles>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var particles = SystemAPI.GetSingletonBuffer<GameManager.Particles>(true);
        var particleOnDamageLookup = SystemAPI.GetComponentLookup<ParticleOnDamage>(true);
        var networkIdMapping = SystemAPI.GetSingleton<NetworkIdMapping>();
        foreach (var (transform, projE) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<ProjectileHit>().WithEntityAccess())
        {
            if (!SystemAPI.HasBuffer<ProjectileHitEntity>(projE)) continue;
            
            var hit = SystemAPI.GetBuffer<ProjectileHitEntity>(projE);
            for (int i = 0; i < hit.Length; i++)
            {
                if (!particleOnDamageLookup.TryGetRefRO(networkIdMapping[hit[i].Value], out var particleOnDamage)) continue;
                if (particleOnDamage.ValueRO.ParticleIndex < 0 || particleOnDamage.ValueRO.ParticleIndex >= particles.Length) continue;
                var particlePrefab = particles[particleOnDamage.ValueRO.ParticleIndex];
                var particle = particlePrefab.Prefab.Value.GetFromPool();
                particle.transform.SetPositionAndRotation(transform.ValueRO.Position, transform.ValueRO.Rotation);
            }
        }
    }
}