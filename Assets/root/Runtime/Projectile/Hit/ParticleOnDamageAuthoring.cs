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
            AddComponent(entity, new ParticleOnDamage(){ ParticleIndex = authoring.Particle.AssetIndex });
        }
    }
}

[UpdateInGroup(typeof(ProjectileSystemGroup))]
public partial struct ProjectileHitSystem_Particle : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameManager.Particles>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var particles = SystemAPI.GetSingletonBuffer<GameManager.Particles>(true);
        var particleOnDamageLookup = SystemAPI.GetComponentLookup<ParticleOnDamage>(true);
        foreach (var (hit, transform) in SystemAPI.Query<RefRO<ProjectileHit>, RefRO<LocalTransform>>())
        {
            if (!particleOnDamageLookup.TryGetRefRO(hit.ValueRO.HitEntity, out var particleOnDamage)) continue;
            if (particleOnDamage.ValueRO.ParticleIndex < 0 || particleOnDamage.ValueRO.ParticleIndex >= particles.Length) continue;
            var particlePrefab = particles[particleOnDamage.ValueRO.ParticleIndex];
            var particle = particlePrefab.Prefab.Value.GetFromPool();
            particle.transform.SetPositionAndRotation(transform.ValueRO.Position, transform.ValueRO.Rotation);
        }
    }
}