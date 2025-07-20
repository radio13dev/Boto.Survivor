using NativeTrees;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using AABB = NativeTrees.AABB;

namespace Collisions
{
    [UpdateInGroup(typeof(CollisionSystemGroup))]
    public partial struct PickupHitSurvivorCollisionSystem : ISystem
    {
        NativeTrees.NativeOctree<Entity> m_projectileTree;
        EntityQuery m_projectileQuery;
        EntityQuery m_survivorQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            m_projectileTree = new(
                new(min: new float3(-1000, -1000, -1000), max: new float3(1000, 1000, 1000)),
                Allocator.Persistent
            );

            m_projectileQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Collider>().WithAll<Pickup, DestroyAtTime>().Build();
            state.RequireForUpdate(m_projectileQuery);

            m_survivorQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Collider>().WithAll<SurvivorTag, LinkedEntityGroup>().Build();
            state.RequireForUpdate(m_survivorQuery);
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

            // Wait for that
            state.CompleteDependency();

            // Perform collisions
            var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var parallel = delayedEcb.AsParallelWriter();
            new CollisionJob()
            {
                ecb = parallel,
                tree = m_projectileTree,
                projectileLookup = SystemAPI.GetComponentLookup<DestroyAtTime>(false)
            }.Schedule(m_survivorQuery);
        }

        public void OnDestroy(ref SystemState state)
        {
            m_projectileTree.Dispose();
        }

        /// <summary>
        /// Searches for overlaps between entities and their collision targets.
        /// </summary>
        unsafe partial struct CollisionJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            [ReadOnly] public NativeTrees.NativeOctree<Entity> tree;
            public ComponentLookup<DestroyAtTime> projectileLookup;

            unsafe public void Execute([ChunkIndexInQuery] int Key, Entity entity, in LocalTransform transform, in Collider collider, ref DynamicBuffer<LinkedEntityGroup> linkedEntityGroup)
            {
                var adjustedAABB = collider.Add(transform.Position);
                fixed (ComponentLookup<DestroyAtTime>* projectileLookup_ptr = &projectileLookup)
                fixed (DynamicBuffer<LinkedEntityGroup>* linked_ptr = &linkedEntityGroup) 
                {
                    var visitor = new CollisionVisitor(Key, entity, ref ecb, projectileLookup_ptr, linked_ptr);
                    tree.Range(adjustedAABB, ref visitor);
                    visitor.Dispose();
                }
            }

            public unsafe struct CollisionVisitor : IOctreeRangeVisitor<Entity>
            {
                readonly Entity _parent;
                readonly int _key;
                EntityCommandBuffer.ParallelWriter _ecb;
                NativeParallelHashSet<Entity> _ignoredCollisions;
                ComponentLookup<DestroyAtTime>* _projectileLookup;
                DynamicBuffer<LinkedEntityGroup>* _linked;

                public CollisionVisitor(int key, Entity parent, ref EntityCommandBuffer.ParallelWriter ecb, ComponentLookup<DestroyAtTime>* projectileLookup, DynamicBuffer<LinkedEntityGroup>* linked)
                {
                    _key = key;
                    _parent = parent;
                    _ecb = ecb;
                    _projectileLookup = projectileLookup;
                    _linked = linked;
                    _ignoredCollisions = new NativeParallelHashSet<Entity>(4, Allocator.TempJob);
                }

                public bool OnVisit(Entity projectile, AABB objBounds, AABB queryRange)
                {
                    if (!objBounds.Overlaps(queryRange)) return true;

                    if (_ignoredCollisions.Add(projectile))
                    {
                        // Destroy projectiles when they collide (by setting their life to 0)
                        _projectileLookup->GetRefRW(projectile).ValueRW.DestroyTime = 0;
                        _ecb.SetComponentEnabled<ProjectileSpawner>(_key, _parent, true);
                    }

                    return true;
                }

                public void Dispose()
                {
                    _ignoredCollisions.Dispose();
                }
            }
        }
    }
}