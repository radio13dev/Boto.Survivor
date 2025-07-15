using NativeTrees;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Collisions
{
    [UpdateInGroup(typeof(CollisionSystemGroup))]
    public partial struct SurvivorShootEnemyCollisionSystem : ISystem
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

            m_projectileQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Collider>().WithAll<ProjectileTag, SurvivorProjectileTag, Movement>().Build();
            state.RequireForUpdate(m_projectileQuery);

            m_survivorQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Collider>().WithAll<EnemyTag, Health, Force>().Build();
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

            // Perform collisions
            var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var parallel = delayedEcb.AsParallelWriter();
            state.Dependency = new CollisionJob()
            {
                ecb = parallel,
                tree = m_projectileTree,
                projectileVelLookup = SystemAPI.GetComponentLookup<Movement>(true)
            }.ScheduleParallel(m_survivorQuery, state.Dependency);
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
            [ReadOnly] public ComponentLookup<Movement> projectileVelLookup;

            unsafe public void Execute([ChunkIndexInQuery] int Key, Entity entity, in LocalTransform transform, in Collider collider, ref Health health, ref Force movement)
            {
                var adjustedAABB2D = collider.Add(transform.Position.xy);
                fixed (Health* health_ptr = &health)
                fixed (Force* movement_ptr = &movement)
                fixed (ComponentLookup<Movement>* projectileVelLookup_ptr = &projectileVelLookup)
                {
                    var visitor = new CollisionVisitor(Key, ref ecb, transform, health_ptr, movement_ptr, projectileVelLookup_ptr);
                    tree.Range(adjustedAABB2D, ref visitor);
                    visitor.Dispose();
                }
            }

            public unsafe struct CollisionVisitor : IQuadtreeRangeVisitor<Entity>
            {
                readonly int _key;
                EntityCommandBuffer.ParallelWriter _ecb;
                LocalTransform _sourceTransform;
                Health* _health;
                Force* _movement;
                NativeParallelHashSet<Entity> _ignoredCollisions;
                ComponentLookup<Movement>* _projectileVelLookup;

                public CollisionVisitor(int key, ref EntityCommandBuffer.ParallelWriter ecb, LocalTransform sourceTransform, Health* health, Force* movement,
                    ComponentLookup<Movement>* projectileVelLookup)
                {
                    _key = key;
                    _ecb = ecb;
                    _sourceTransform = sourceTransform;
                    _health = health;
                    _movement = movement;
                    _projectileVelLookup = projectileVelLookup;
                    _ignoredCollisions = new NativeParallelHashSet<Entity>(4, Allocator.TempJob);
                }

                public bool OnVisit(Entity projectile, AABB2D objBounds, AABB2D queryRange)
                {
                    if (!objBounds.Overlaps(queryRange)) return true;

                    if (_ignoredCollisions.Add(projectile))
                    {
                        // Destroy projectiles when they collide (by setting their life to 0)
                        _ecb.SetComponent(_key, projectile, new DestroyAtTime());
                        _health->Value -= 1;
                        _movement->Velocity += _projectileVelLookup->GetRefRO(projectile).ValueRO.Velocity;
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