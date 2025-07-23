using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[Save]
public struct ParticleOnDestroy : IComponentData
{
    public int ParticleIndex;
}

public class ParticleOnDestroyAuthoring : MonoBehaviour
{
    public DatabaseRef<PooledParticle, ParticleDatabase> Particle = new();

    public class Baker : Baker<ParticleOnDestroyAuthoring>
    {
        public override void Bake(ParticleOnDestroyAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new ParticleOnDestroy(){ ParticleIndex = authoring.Particle.AssetIndex });
        }
    }
}

[UpdateBefore(typeof(DestroySystem))]
[UpdateInGroup(typeof(DestroySystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
public partial struct ParticleOnDestroySystem : ISystem
{
    EntityQuery m_CleanupQuery;

    public void OnCreate(ref SystemState state)
    {
        m_CleanupQuery = SystemAPI.QueryBuilder().WithAll<ParticleOnDestroy, DestroyFlag>().Build();
        state.RequireForUpdate(m_CleanupQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        var particles = SystemAPI.GetSingletonBuffer<GameManager.Particles>();
        foreach (var (onDestroy, transform, movement) in SystemAPI.Query<RefRO<ParticleOnDestroy>, RefRO<LocalTransform>, RefRO<Movement>>()
            .WithAll<DestroyFlag>()
            )
        {
            if (onDestroy.ValueRO.ParticleIndex < 0 || onDestroy.ValueRO.ParticleIndex >= particles.Length) continue;
            var particlePrefab = particles[onDestroy.ValueRO.ParticleIndex];
            var particle = particlePrefab.Prefab.Value.GetFromPool();
            particle.transform.SetPositionAndRotation(transform.ValueRO.Position, transform.ValueRO.Rotation);
        }
        foreach (var (onDestroy, transform, movement) in SystemAPI.Query<RefRO<ParticleOnDestroy>, RefRO<LocalTransform>, RefRO<SurfaceMovement>>()
            .WithAll<DestroyFlag>()
            )
        {
            if (onDestroy.ValueRO.ParticleIndex < 0 || onDestroy.ValueRO.ParticleIndex >= particles.Length) continue;
            var particlePrefab = particles[onDestroy.ValueRO.ParticleIndex];
            var particle = particlePrefab.Prefab.Value.GetFromPool();
            particle.transform.SetPositionAndRotation(transform.ValueRO.Position, transform.ValueRO.Rotation);
        }
        foreach (var (onDestroy, transform) in SystemAPI.Query<RefRO<ParticleOnDestroy>, RefRO<LocalTransform>>()
            .WithAll<DestroyFlag>()
            .WithNone<Movement, SurfaceMovement>()
            )
        {
            if (onDestroy.ValueRO.ParticleIndex < 0 || onDestroy.ValueRO.ParticleIndex >= particles.Length) continue;
            var particlePrefab = particles[onDestroy.ValueRO.ParticleIndex];
            var particle = particlePrefab.Prefab.Value.GetFromPool();
            particle.transform.SetPositionAndRotation(transform.ValueRO.Position, transform.ValueRO.Rotation);
        }
    }
}