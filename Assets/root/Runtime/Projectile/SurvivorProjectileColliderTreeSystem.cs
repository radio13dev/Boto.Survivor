/*
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
    [UpdateInGroup(typeof(CollisionSystemGroup))]
    public partial struct SurvivorProjectileColliderTreeSystem : ISystem
    {
        NativeTrees.NativeOctree<Entity> m_projectileTree;
        EntityQuery m_projectileQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            m_projectileTree = new(
                new(min: new float3(-1000, -1000, -1000), max: new float3(1000, 1000, 1000)),
                Allocator.Persistent
            );

            m_projectileQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Collider>().WithAll<ProjectileTag, SurvivorProjectileTag, SurfaceMovement>().Build();
            state.RequireForUpdate(m_projectileQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            // Allocate
            var projectileEntities = m_projectileQuery.ToEntityArray(allocator: Allocator.TempJob);
            var projectileColliders = m_projectileQuery.ToComponentDataArray<Collider>(allocator: Allocator.TempJob);
            var projectileTransforms = m_projectileQuery.ToComponentDataArray<LocalTransform>(allocator: Allocator.TempJob);
        
            // Update trees
            state.Dependency = new RegenerateJob()
            {
                tree = m_projectileTree,
                entities = projectileEntities,
                colliders = projectileColliders,
                transforms = projectileTransforms
            }.Schedule(state.Dependency);
            projectileEntities.Dispose(state.Dependency);
            projectileColliders.Dispose(state.Dependency);
            projectileTransforms.Dispose(state.Dependency);

            // Perform collisions
            var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var parallel = delayedEcb.AsParallelWriter();
            state.Dependency = new CollisionJob()
            {
                ecb = parallel,
                tree = m_projectileTree,
                projectileVelLookup = SystemAPI.GetComponentLookup<SurfaceMovement>(true)
            }.ScheduleParallel(state.Dependency);
        }

        public void OnDestroy(ref SystemState state)
        {
            m_projectileTree.Dispose();
        }

        /// <summary>
        /// Searches for overlaps between entities and their collision targets.
        /// </summary>
        [BurstCompile]
        [WithAll(typeof(EnemyTag))]
        unsafe partial struct CollisionJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            [ReadOnly] public NativeTrees.NativeOctree<Entity> tree;
            [ReadOnly] public ComponentLookup<SurfaceMovement> projectileVelLookup;

            unsafe public void Execute([ChunkIndexInQuery] int Key, Entity entity, in LocalTransform transform, in Collider collider)
            {
                var adjustedAABB2D = collider.Add(transform.Position);
                var visitor = new CollisionVisitor(Key, ref ecb, entity);
                tree.Range(adjustedAABB2D, ref visitor);
            }

            [BurstCompile]
            unsafe struct CollisionVisitor : IOctreeRangeVisitor<Entity>
            {
                readonly int _key;
                EntityCommandBuffer.ParallelWriter _ecb;
                Entity _enemyE;

                public CollisionVisitor(int key, ref EntityCommandBuffer.ParallelWriter ecb, Entity enemyE)
                {
                    _key = key;
                    _ecb = ecb;
                    _enemyE = enemyE;
                }

                public bool OnVisit(Entity projectile, AABB objBounds, AABB queryRange)
                {
                    if (!objBounds.Overlaps(queryRange)) return true;

                    _ecb.SetComponent(_key, projectile, new ProjectileHit(){ HitEntity = _enemyE });
                    return false;
                }
            }
        }
    }
}
*/