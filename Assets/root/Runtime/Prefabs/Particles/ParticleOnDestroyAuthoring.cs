using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public struct ParticleOnDestroy : IComponentData
{
    public int ParticleIndex;
}

public class ParticleOnDestroyAuthoring : MonoBehaviour
{
    public DatabaseRef<ParticleAuthoring, ParticleDatabase> Particle;

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
    }

    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer ecb;
        [ReadOnly] public NativeArray<GameManager.Particles> particles;

        public void Execute(in ParticleOnDestroy onDestroy, in LocalTransform transform, in Movement movement)
        {
            var newParticle = ecb.Instantiate(particles[onDestroy.ParticleIndex].Prefab);
            ecb.SetComponent(newParticle, transform);
            ecb.SetComponent(newParticle, new Force() { Velocity = movement.Velocity });
        }
    }
}