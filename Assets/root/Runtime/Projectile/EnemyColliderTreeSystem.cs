using System.Threading;
using BovineLabs.Core.Collections;
using BovineLabs.Core.Memory;
using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using AABB = NativeTrees.AABB;

namespace Collisions
{
    public static class EnemyColliderTree
    {
        [BurstCompile]
        public struct NearestVisitor : IOctreeNearestVisitor<(Entity, NetworkId)>
        {
            public int Hits;
            public AABB Nearest;

            public bool OnVist((Entity, NetworkId) obj, AABB bounds)
            {
                Hits++;
                Nearest = bounds;
                return false; // End checks
            }
        }

        [BurstCompile]
        public struct DistanceProvider : IOctreeDistanceProvider<(Entity, NetworkId)>
        {
            public float DistanceSquared(float3 point, (Entity, NetworkId) obj, AABB bounds)
            {
                return math.distancesq(point, bounds.Center);
            }
        }
    }

    [UpdateInGroup(typeof(CollisionSystemGroup))]
    [BurstCompile]
    public unsafe partial struct EnemyColliderTreeSystem : ISystem
    {
        public NativeOctree<(Entity, NetworkId)> Tree;
        public EntityQuery Query;
        
        public void OnCreate(ref SystemState state)
        {
            Tree = new(
                new(min: new float3(-1000, -1000, -1000), max: new float3(1000, 1000, 1000)),
                Allocator.Persistent
            );
            Query = SystemAPI.QueryBuilder().WithAll<NetworkId, LocalTransform, Collider>().WithAll<EnemyTag>().Build();

            state.RequireForUpdate<GameManager.Resources>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnDestroy(ref SystemState state)
        {
            Tree.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Allocate
            var enemyEntities = Query.ToEntityArray(allocator: Allocator.TempJob);
            var enemyNetworkIds = Query.ToComponentDataArray<NetworkId>(allocator: Allocator.TempJob);
            var enemyColliders = Query.ToComponentDataArray<Collider>(allocator: Allocator.TempJob);
            var enemyTransforms = Query.ToComponentDataArray<LocalTransform>(allocator: Allocator.TempJob);
        
            // Update trees
            state.Dependency = new RegenerateJob_NetworkId()
            {
                tree = Tree,
                networkIds = enemyNetworkIds,
                entities = enemyEntities,
                colliders = enemyColliders,
                transforms = enemyTransforms 
            }.Schedule(state.Dependency);
            enemyEntities.Dispose(state.Dependency);
            enemyColliders.Dispose(state.Dependency);
            enemyTransforms.Dispose(state.Dependency);

            // Perform collisions
            var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var parallel = delayedEcb.AsParallelWriter();
            
            var b = new EnemyPushForceJob()
            {
                tree = Tree
            }.ScheduleParallel(state.Dependency);
            
            var c = new ProjectileHitEnemyJob()
            {
                tree = Tree
            }.ScheduleParallel(state.Dependency);
            
            state.Dependency = JobHandle.CombineDependencies(b,c);
        }

        [BurstCompile]
        unsafe partial struct EnemyPushForceJob : IJobEntity
        {
            [ReadOnly] public NativeTrees.NativeOctree<(Entity, NetworkId)> tree;

            unsafe public void Execute([EntityIndexInChunk] int Key, Entity entity, in LocalTransform transform, in Collider collider, ref Force force)
            {
                var adjustedAABB2D = collider.Add(transform.Position);
                fixed (Force* force_ptr = &force)
                {
                    var visitor = new CollisionVisitor(entity, force_ptr);
                    tree.Range(adjustedAABB2D, ref visitor);
                }
            }


            public unsafe struct CollisionVisitor : IOctreeRangeVisitor<(Entity, NetworkId)>
            {
                Entity _source;
                Force* _force;

                public CollisionVisitor(Entity source, Force* force)
                {
                    _source = source;
                    _force = force;
                }

                public bool OnVisit((Entity, NetworkId) projectile, AABB objBounds, AABB queryRange)
                {
                    if (_source == projectile.Item1) return true;
                    if (!objBounds.Overlaps(queryRange)) return true;
                    
                    _force->Velocity += (queryRange.Center - objBounds.Center)/2;
                    return true;
                }
            }
        }
        
        [BurstCompile]
        [WithAll(typeof(SurvivorProjectileTag))]
        [WithDisabled(typeof(ProjectileHit))]
        unsafe partial struct ProjectileHitEnemyJob : IJobEntity
        {
            [ReadOnly] public NativeTrees.NativeOctree<(Entity, NetworkId)> tree;

            public unsafe void Execute([EntityIndexInChunk] int Key, Entity projectileE, in LocalTransform transform, in Collider collider, 
               in DynamicBuffer<ProjectileIgnoreEntity> projectileIgnoreEntities, ref DynamicBuffer<ProjectileHitEntity> projectileHitEntities, EnabledRefRW<ProjectileHit> projectileHitState)
            {
                var adjustedAABB2D = collider.Add(transform.Position);
                fixed (DynamicBuffer<ProjectileHitEntity>* projectileHit_ptr = &projectileHitEntities)
                {
                    var visitor = new CollisionVisitor(projectileE, projectileIgnoreEntities, projectileHit_ptr, projectileHitState);
                    tree.Range(adjustedAABB2D, ref visitor);
                }
            }

            public unsafe struct CollisionVisitor : IOctreeRangeVisitor<(Entity, NetworkId)>
            {
                Entity _projectileE;
                DynamicBuffer<ProjectileIgnoreEntity> _projectileIgnore_ptr;
                DynamicBuffer<ProjectileHitEntity>* _projectileHit_ptr;
                EnabledRefRW<ProjectileHit> _projectileHitState;

                public CollisionVisitor(Entity projectileE, 
                    DynamicBuffer<ProjectileIgnoreEntity> projectileIgnore_ptr, DynamicBuffer<ProjectileHitEntity>* projectileHit_ptr, 
                    EnabledRefRW<ProjectileHit> projectileHitState)
                {
                    _projectileE = projectileE;
                    _projectileIgnore_ptr = projectileIgnore_ptr;
                    _projectileHit_ptr = projectileHit_ptr;
                    _projectileHitState = projectileHitState;
                }

                public bool OnVisit((Entity, NetworkId) enemyE, AABB objBounds, AABB queryRange)
                {
                    if (!objBounds.Overlaps(queryRange)) return true;
                    
                    for (int i = 0; i < _projectileIgnore_ptr.Length; i++)
                    {
                        if (_projectileIgnore_ptr[i].Value == enemyE.Item2)
                        {
                            return true; // Ignore this entity
                        }
                    }
                    _projectileHit_ptr->Add(new ProjectileHitEntity(enemyE.Item2));
                    _projectileHitState.ValueRW = true;
                    return true;
                }
            }
        }
    }
}