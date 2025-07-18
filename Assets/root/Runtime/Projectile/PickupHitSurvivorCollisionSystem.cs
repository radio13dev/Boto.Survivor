using NativeTrees;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Collisions
{
    [UpdateInGroup(typeof(CollisionSystemGroup))]
    public partial struct PickupHitSurvivorCollisionSystem : ISystem
    {
        NativeTrees.NativeQuadtree<Entity> m_projectileTree;
        EntityQuery m_projectileQuery;
        EntityQuery m_survivorQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            m_projectileTree = new(
                new(min: new float2(-1000, -1000), max: new float2(1000, 1000)),
                Allocator.Persistent
            );

            m_projectileQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform2D, Collider>().WithAll<Pickup, DestroyAtTime>().Build();
            state.RequireForUpdate(m_projectileQuery);

            m_survivorQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform2D, Collider>().WithAll<SurvivorTag, LinkedEntityGroup>().Build();
            state.RequireForUpdate(m_survivorQuery);
        }

        NativeArray<Entity> m_ProjectileQueryEntities;
        NativeArray<Collider> m_ProjectileQueryColliders;
        NativeArray<LocalTransform2D> m_ProjectileQueryTransforms;

        public void OnUpdate(ref SystemState state)
        {
            m_ProjectileQueryEntities.Dispose();
            m_ProjectileQueryColliders.Dispose();
            m_ProjectileQueryTransforms.Dispose();
            
            // Update trees
            state.Dependency = new RegenerateJob()
            {
                tree = m_projectileTree,
                entities = m_ProjectileQueryEntities = m_projectileQuery.ToEntityArray(allocator: Allocator.TempJob),
                colliders = m_ProjectileQueryColliders = m_projectileQuery.ToComponentDataArray<Collider>(allocator: Allocator.TempJob),
                transforms = m_ProjectileQueryTransforms = m_projectileQuery.ToComponentDataArray<LocalTransform2D>(allocator: Allocator.TempJob)
            }.Schedule(state.Dependency);

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
            m_ProjectileQueryEntities.Dispose();
            m_ProjectileQueryColliders.Dispose();
            m_ProjectileQueryTransforms.Dispose();
            m_projectileTree.Dispose();
        }

        partial struct RegenerateJob : IJob
        {
            public NativeTrees.NativeQuadtree<Entity> tree;
            [ReadOnly] public NativeArray<Entity> entities;
            [ReadOnly] public NativeArray<Collider> colliders;
            [ReadOnly] public NativeArray<LocalTransform2D> transforms;

            public void Execute()
            {
                tree.Clear();
                for (int i = 0; i < colliders.Length; i++)
                    tree.Insert(entities[i], colliders[i].Add(transforms[i].Position));
            }
        }

        /// <summary>
        /// Searches for overlaps between entities and their collision targets.
        /// </summary>
        unsafe partial struct CollisionJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            [ReadOnly] public NativeTrees.NativeQuadtree<Entity> tree;
            public ComponentLookup<DestroyAtTime> projectileLookup;

            unsafe public void Execute([ChunkIndexInQuery] int Key, Entity entity, in LocalTransform2D transform, in Collider collider, ref DynamicBuffer<LinkedEntityGroup> linkedEntityGroup)
            {
                var adjustedAABB2D = collider.Add(transform.Position.xy);
                fixed (ComponentLookup<DestroyAtTime>* projectileLookup_ptr = &projectileLookup)
                fixed (DynamicBuffer<LinkedEntityGroup>* linked_ptr = &linkedEntityGroup) 
                {
                    var visitor = new CollisionVisitor(Key, entity, ref ecb, projectileLookup_ptr, linked_ptr);
                    tree.Range(adjustedAABB2D, ref visitor);
                    visitor.Dispose();
                }
            }

            public unsafe struct CollisionVisitor : IQuadtreeRangeVisitor<Entity>
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

                public bool OnVisit(Entity projectile, AABB2D objBounds, AABB2D queryRange)
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