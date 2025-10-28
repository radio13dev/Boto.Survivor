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

    public struct TerrainTag : IComponentData{}
    public struct IgnoresTerrain : IComponentData{}

    [UpdateInGroup(typeof(CollisionSystemGroup))]
    [UpdateAfter(typeof(EnemyColliderTreeSystem))]
    public partial struct TerrainCollisionSystem : ISystem
    {
        int m_LastEntityCount;
        NativeTrees.NativeOctree<(Entity e, Collider c)> m_Tree;
        EntityQuery m_TreeQuery;

        public void OnCreate(ref SystemState state)
        {
            m_Tree = new(
                new(min: new float3(-350, -200, -350), max: new float3(350, 200, 350)),
                Allocator.Persistent
            );

            m_TreeQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Collider>().WithAll<TerrainTag>().Build();
            state.RequireForUpdate(m_TreeQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityCount = m_TreeQuery.CalculateEntityCount();
            if (entityCount != m_LastEntityCount)
            {
                m_LastEntityCount = entityCount;
                var entities = m_TreeQuery.ToEntityArray(allocator: Allocator.TempJob);
                var colliders = m_TreeQuery.ToComponentDataArray<Collider>(allocator: Allocator.TempJob);
                var transforms = m_TreeQuery.ToComponentDataArray<LocalTransform>(allocator: Allocator.TempJob);
                state.Dependency = new RegenerateJob_Collider()
                {
                    tree = m_Tree,
                    entities = entities,
                    colliders = colliders,
                    transforms = transforms
                }.Schedule(state.Dependency);
                entities.Dispose(state.Dependency);
                colliders.Dispose(state.Dependency);
                transforms.Dispose(state.Dependency);
            }

            // Perform collisions
            var a = new CharacterTerrainCollisionJob()
            {
                tree = m_Tree,
            }.ScheduleParallel(state.Dependency);
            var b = new ProjectileTerrainCollisionJob()
            {
                tree = m_Tree,
            }.ScheduleParallel(state.Dependency);
            
            state.Dependency = JobHandle.CombineDependencies(a,b);
        }

        public void OnDestroy(ref SystemState state)
        {
            m_Tree.Dispose();
        }

        /// <summary>
        /// Searches for overlaps between entities and their collision targets.
        /// </summary>
        [WithNone(typeof(IgnoresTerrain))]
        internal unsafe partial struct CharacterTerrainCollisionJob : IJobEntity
        {
            [ReadOnly] public NativeTrees.NativeOctree<(Entity e, Collider c)> tree;

            unsafe public void Execute(in LocalTransform transform, in Collider collider, ref Force force)
            {
                var adjustedAABB = collider.Add(transform);
                fixed (Force* force_ptr = &force)
                {
                    var visitor = new CollisionVisitor((transform, collider), force_ptr);
                    tree.Range(adjustedAABB, ref visitor);
                }
            }

            public unsafe struct CollisionVisitor : IOctreeRangeVisitor<(Entity e, Collider c)>
            {
                LazyCollider _collider;
                Force* _force;

                public CollisionVisitor(LazyCollider collider, Force* force)
                {
                    _collider = collider;
                    _force = force;
                }

                public bool OnVisit((Entity e, Collider c) terrain, AABB objBounds, AABB queryRange)
                {
                    if (!terrain.c.Overlaps(_collider)) return true;

                    var pointOnColliderSurface = terrain.c.GetPointOnSurface(_collider);
                    _force->Shift += pointOnColliderSurface - queryRange.Center;
                    return true;
                }
            }
        }
        
        
        [WithAll(typeof(Projectile))]
        [WithNone(typeof(IgnoresTerrain))]
        internal unsafe partial struct ProjectileTerrainCollisionJob : IJobEntity
        {
            [ReadOnly] public NativeTrees.NativeOctree<(Entity e, Collider c)> tree;

            unsafe public void Execute(in LocalTransform transform, in Collider collider, ref DestroyAtTime projectileLifespan)
            {
                var adjustedAABB2D = collider.Add(transform);
                fixed (DestroyAtTime* lifespan_ptr = &projectileLifespan)
                {
                    var visitor = new CollisionVisitor((transform, collider), lifespan_ptr);
                    tree.Range(adjustedAABB2D, ref visitor);
                }
            }

            public unsafe struct CollisionVisitor : IOctreeRangeVisitor<(Entity e, Collider c)>
            {
                LazyCollider _collider;
                DestroyAtTime* _lifespanPtr;

                public CollisionVisitor(LazyCollider collider, DestroyAtTime* lifespanPtr)
                {
                    _collider = collider;
                    _lifespanPtr = lifespanPtr;
                }

                public bool OnVisit((Entity e, Collider c) terrain, AABB objBounds, AABB queryRange)
                {
                    if (!terrain.c.Overlaps(_collider)) return true;
                    _lifespanPtr->DestroyTime = 0;
                    return false;
                }
            }
        }
    }
}