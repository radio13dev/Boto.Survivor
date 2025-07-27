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

    [UpdateInGroup(typeof(CollisionSystemGroup))]
    [UpdateAfter(typeof(EnemyColliderTreeSystem))]
    public partial struct TerrainCollisionSystem : ISystem
    {
        int m_LastEntityCount;
        NativeTrees.NativeOctree<Entity> m_Tree;
        EntityQuery m_TreeQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            m_Tree = new(
                new(min: new float3(-1000, -1000, -1000), max: new float3(1000, 1000, 1000)),
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
        [BurstCompile]
        unsafe partial struct CharacterTerrainCollisionJob : IJobEntity
        {
            [ReadOnly] public NativeTrees.NativeOctree<Entity> tree;

            unsafe public void Execute(in LocalTransform transform, in Collider collider, ref Force force)
            {
                var adjustedAABB = collider.Add(transform.Position);
                fixed (Force* force_ptr = &force)
                {
                    var visitor = new CollisionVisitor(force_ptr);
                    tree.Range(adjustedAABB, ref visitor);
                }
            }

            [BurstCompile]
            public unsafe struct CollisionVisitor : IOctreeRangeVisitor<Entity>
            {
                Force* _force;

                public CollisionVisitor(Force* force)
                {
                    _force = force;
                }

                public bool OnVisit(Entity treeEntity, AABB objBounds, AABB queryRange)
                {
                    if (!objBounds.Overlaps(queryRange)) return true;
                    
                    // TODO: Fix this now that it uses 3D AABB
                    var delta = queryRange.Center - objBounds.Center;
                    var halfSizeA = (objBounds.max - objBounds.min) * 0.5f;
                    var halfSizeB = (queryRange.max - queryRange.min) * 0.5f;

                    float overlapX = halfSizeA.x + halfSizeB.x - math.abs(delta.x);
                    float overlapY = halfSizeA.y + halfSizeB.y - math.abs(delta.y);
                    float overlapZ = halfSizeA.z + halfSizeB.z - math.abs(delta.z);

                    if (overlapX < overlapY && overlapX < overlapZ)
                    {
                        float pushX = overlapX * (delta.x < 0f ? -1f : 1f);
                        _force->Shift += new float3(pushX, 0f, 0f);
                    }
                    else if (overlapY < overlapX && overlapY < overlapZ)
                    {
                        float pushY = overlapY * (delta.y < 0f ? -1f : 1f);
                        _force->Shift += new float3(0f, pushY, 0);
                    }
                    else
                    {
                        float pushZ = overlapZ * (delta.z < 0f ? -1f : 1f);
                        _force->Shift += new float3(0f, 0f, pushZ);
                    }
                    return true;
                }
            }
        }
        
        
        [BurstCompile]
        [WithAll(typeof(Projectile))]
        unsafe partial struct ProjectileTerrainCollisionJob : IJobEntity
        {
            [ReadOnly] public NativeTrees.NativeOctree<Entity> tree;

            unsafe public void Execute(in LocalTransform transform, in Collider collider, ref DestroyAtTime projectileLifespan)
            {
                var adjustedAABB2D = collider.Add(transform.Position);
                fixed (DestroyAtTime* lifespan_ptr = &projectileLifespan)
                {
                    var visitor = new CollisionVisitor(lifespan_ptr);
                    tree.Range(adjustedAABB2D, ref visitor);
                }
            }

            [BurstCompile]
            public unsafe struct CollisionVisitor : IOctreeRangeVisitor<Entity>
            {
                DestroyAtTime* _lifespanPtr;

                public CollisionVisitor(DestroyAtTime* lifespanPtr)
                {
                    _lifespanPtr = lifespanPtr;
                }

                public bool OnVisit(Entity treeEntity, AABB objBounds, AABB queryRange)
                {
                    if (!objBounds.Overlaps(queryRange)) return true;
                    _lifespanPtr->DestroyTime = 0;
                    return false;
                }
            }
        }
    }
}