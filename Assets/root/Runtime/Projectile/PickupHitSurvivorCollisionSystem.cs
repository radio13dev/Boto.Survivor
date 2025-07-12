using NativeTrees;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Collisions
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
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

            m_projectileQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Collider>().WithAll<Pickup, DestroyAtTime>().Build();
            state.RequireForUpdate(m_projectileQuery);

            m_survivorQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Collider>().WithAll<SurvivorTag, LinkedEntityGroup>().Build();
            state.RequireForUpdate(m_survivorQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            // Update trees
            var projectile_entities = m_projectileQuery.ToEntityArray(allocator: Allocator.TempJob);
            var projectile_colliders = m_projectileQuery.ToComponentDataArray<Collider>(allocator: Allocator.TempJob);
            var projectile_transforms = m_projectileQuery.ToComponentDataArray<LocalTransform>(allocator: Allocator.TempJob);
            state.Dependency = new RegenerateJob()
            {
                tree = m_projectileTree,
                entities = projectile_entities,
                colliders = projectile_colliders,
                transforms = projectile_transforms
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

            projectile_entities.Dispose();
            projectile_colliders.Dispose();
            projectile_transforms.Dispose();
        }

        partial struct RegenerateJob : IJob
        {
            public NativeTrees.NativeQuadtree<Entity> tree;
            [ReadOnly] public NativeArray<Entity> entities;
            [ReadOnly] public NativeArray<Collider> colliders;
            [ReadOnly] public NativeArray<LocalTransform> transforms;

            public void Execute()
            {
                tree.Clear();
                for (int i = 0; i < colliders.Length; i++)
                    tree.Insert(entities[i], colliders[i].Add(transforms[i].Position.xy));
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

            unsafe public void Execute([ChunkIndexInQuery] int Key, Entity entity, in LocalTransform transform, in Collider collider, ref DynamicBuffer<LinkedEntityGroup> linkedEntityGroup)
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