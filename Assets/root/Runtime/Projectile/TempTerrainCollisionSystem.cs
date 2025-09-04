using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using AABB = NativeTrees.AABB;

namespace Collisions
{
    public struct TempTerrainTag : IComponentData{}

    [UpdateInGroup(typeof(CollisionSystemGroup))]
    [UpdateAfter(typeof(EnemyColliderTreeSystem))]
    public partial struct TempTerrainCollisionSystem : ISystem
    {
        NativeTrees.NativeOctree<Entity> m_Tree;
        EntityQuery m_TreeQuery;

        public void OnCreate(ref SystemState state)
        {
            m_Tree = new(
                new(min: new float3(-1000, -1000, -1000), max: new float3(1000, 1000, 1000)),
                Allocator.Persistent
            );

            m_TreeQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Collider>().WithAll<TempTerrainTag>().Build();
            state.RequireForUpdate(m_TreeQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var entities = m_TreeQuery.ToEntityArray(allocator: Allocator.TempJob);
            var colliders = m_TreeQuery.ToComponentDataArray<Collider>(allocator: Allocator.TempJob);
            var transforms = m_TreeQuery.ToComponentDataArray<LocalTransform>(allocator: Allocator.TempJob);
            state.Dependency = new RegenerateJob()
            {
                tree = m_Tree,
                entities = entities,
                colliders = colliders,
                transforms = transforms
            }.Schedule(state.Dependency);
            entities.Dispose(state.Dependency);
            colliders.Dispose(state.Dependency);
            transforms.Dispose(state.Dependency);

            if (entities.Length > 0)
            {
                // Perform collisions
                var a = new TerrainCollisionSystem.CharacterTerrainCollisionJob()
                {
                    tree = m_Tree,
                }.ScheduleParallel(state.Dependency);
                var b = new TerrainCollisionSystem.ProjectileTerrainCollisionJob()
                {
                    tree = m_Tree,
                }.ScheduleParallel(state.Dependency);
            
                state.Dependency = JobHandle.CombineDependencies(a,b);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            m_Tree.Dispose();
        }
    }
}