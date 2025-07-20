using System;
using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace Collisions
{
    public partial struct TerrainInitSystem : ISystem
    {
        public const int k_TerrainSpawnAttempts = 5000;
    
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameManager.Terrain>();
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // Setup map with initial terrain
            var bounds = TorusMapper.MapBounds;
            var options = SystemAPI.GetSingletonBuffer<GameManager.Terrain>();
            Random r = Random.CreateFromIndex(unchecked((uint)DateTime.UtcNow.Ticks));
            for (int i = 0; i < k_TerrainSpawnAttempts; i++)
            {
                // Todo
                fix
                var pos = r.NextFloat2(bounds.Min, bounds.Max);
                var template = options[r.NextInt(options.Length)];
                var newTerrainE = state.EntityManager.Instantiate(template.Entity);
                state.EntityManager.SetComponentData(newTerrainE, new LocalTransform(){ Position = pos });
            }
            
            state.Enabled = false;
        }
    }

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
            state.Dependency = new CharacterTerrainCollisionJob()
            {
                tree = m_Tree,
            }.Schedule(state.Dependency);
            state.Dependency = new ProjectileTerrainCollisionJob()
            {
                tree = m_Tree,
            }.Schedule(state.Dependency);
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
                    
                    // TODO
                    fix
                    float2 delta = queryRange.Center - objBounds.Center;
                    float2 halfSizeA = (objBounds.max - objBounds.min) * 0.5f;
                    float2 halfSizeB = (queryRange.max - queryRange.min) * 0.5f;

                    float overlapX = halfSizeA.x + halfSizeB.x - math.abs(delta.x);
                    float overlapY = halfSizeA.y + halfSizeB.y - math.abs(delta.y);

                    if (overlapX < overlapY)
                    {
                        float pushX = overlapX * (delta.x < 0f ? -1f : 1f);
                        _force->Shift += new float2(pushX, 0f);
                    }
                    else
                    {
                        float pushY = overlapY * (delta.y < 0f ? -1f : 1f);
                        _force->Shift += new float2(0f, pushY);
                    }
                    return true;
                }
            }
        }
        
        
        [BurstCompile]
        [WithAll(typeof(ProjectileTag))]
        unsafe partial struct ProjectileTerrainCollisionJob : IJobEntity
        {
            [ReadOnly] public NativeTrees.NativeQuadtree<Entity> tree;

            unsafe public void Execute(in LocalTransform2D transform, in Collider collider, ref DestroyAtTime projectileLifespan)
            {
                var adjustedAABB2D = collider.Add(transform.Position.xy);
                fixed (DestroyAtTime* lifespan_ptr = &projectileLifespan)
                {
                    var visitor = new CollisionVisitor(lifespan_ptr);
                    tree.Range(adjustedAABB2D, ref visitor);
                }
            }

            [BurstCompile]
            public unsafe struct CollisionVisitor : IQuadtreeRangeVisitor<Entity>
            {
                DestroyAtTime* _lifespanPtr;

                public CollisionVisitor(DestroyAtTime* lifespanPtr)
                {
                    _lifespanPtr = lifespanPtr;
                }

                public bool OnVisit(Entity treeEntity, AABB2D objBounds, AABB2D queryRange)
                {
                    if (!objBounds.Overlaps(queryRange)) return true;
                    _lifespanPtr->DestroyTime = 0;
                    return false;
                }
            }
        }
    }
}