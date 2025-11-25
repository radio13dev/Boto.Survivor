using System;
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

    public struct TerrainTag : IComponentData
    {
        public TerrainCollisionSystem.Mask Mask;
    }
    public struct CollidesWithTerrain : IComponentData
    {
        public enum AuthoringEnum
        {
            None,
            All,
            IsPlayer,
            IsPlayerProjectile,
            IsPlayerPickup,
            IsEnemy,
            IsEnemyProjectile,
        }
    
        [Flags]
        public enum eMode : byte
        {
            All = 0b11111111,
            IsPlayer = 0b00000001,
            IsPlayerProjectile = 0b00000001,
            IsPlayerPickup = 0b00000001,
            IsEnemy = 0b00000010,
            IsEnemyProjectile = 0b00000010,
        }
        public eMode Mode;
    }
    
    public static class TerrainCollisionSystemExtensions
    {
        public static bool DoesInteract(this TerrainCollisionSystem.Mask terrainMask, CollidesWithTerrain.eMode target)
        {
            return ((byte)terrainMask & (byte)target) != 0;
        }
        
        public static CollidesWithTerrain.eMode ToMask(this CollidesWithTerrain.AuthoringEnum authoring)
        {
            if (!CollidesWithTerrain.eMode.TryParse<CollidesWithTerrain.eMode>(authoring.ToString(), out var mode))
                throw new Exception($"Couldn't parse CollidesWithTerrain.AuthoringEnum {authoring} to CollidesWithTerrain.eMode");
            return mode;
        }
    }

    [UpdateInGroup(typeof(CollisionSystemGroup))]
    [UpdateAfter(typeof(EnemyColliderTreeSystem))]
    public partial struct TerrainCollisionSystem : ISystem
    {
        [Flags]
        public enum Mask : byte
        {
            All = 0b11111111,
            DoesPlayerCollide = 0b00000001,
            DoesPlayerProjectileCollide = 0b00000001,
            DoesPlayerPickupCollide = 0b00000001,
            DoesEnemyCollide = 0b00000010,
            DoesEnemyProjectileCollide = 0b00000010,
        }
    
        int m_LastEntityCount;
        NativeTrees.NativeOctree<(Entity e, Collider c, Mask m)> m_Tree;
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
                var terrains = m_TreeQuery.ToComponentDataArray<TerrainTag>(allocator: Allocator.TempJob);
                state.Dependency = new RegenerateJob_Collider()
                {
                    tree = m_Tree,
                    entities = entities,
                    colliders = colliders,
                    transforms = transforms,
                    terrains = terrains
                }.Schedule(state.Dependency);
                entities.Dispose(state.Dependency);
                colliders.Dispose(state.Dependency);
                transforms.Dispose(state.Dependency);
                terrains.Dispose(state.Dependency);
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
        internal unsafe partial struct CharacterTerrainCollisionJob : IJobEntity
        {
            [ReadOnly] public NativeTrees.NativeOctree<(Entity e, Collider c, Mask m)> tree;

            unsafe public void Execute(in LocalTransform transform, in CollidesWithTerrain collisionInfo, in Collider collider, ref Force force)
            {
                var adjustedAABB = collider.Add(transform);
                fixed (Force* force_ptr = &force)
                {
                    var visitor = new CollisionVisitor((transform, collider), collisionInfo.Mode, force_ptr);
                    tree.Range(adjustedAABB, ref visitor);
                }
            }

            public unsafe struct CollisionVisitor : IOctreeRangeVisitor<(Entity e, Collider c, Mask m)>
            {
                LazyCollider _collider;
                CollidesWithTerrain.eMode _objectCollisionMode;
                Force* _force;

                public CollisionVisitor(LazyCollider collider, CollidesWithTerrain.eMode objectCollisionMode, Force* force)
                {
                    _collider = collider;
                    _objectCollisionMode = objectCollisionMode;
                    _force = force;
                }

                public bool OnVisit((Entity e, Collider c, Mask m) terrain, AABB objBounds, AABB queryRange)
                {
                    if (!terrain.m.DoesInteract(_objectCollisionMode)) return true;
                    if (!terrain.c.Overlaps(_collider)) return true;

                    var pointOnColliderSurface = terrain.c.GetPointOnSurface(_collider);
                    _force->Shift += pointOnColliderSurface - queryRange.Center;
                    return true;
                }
            }
        }
        
        
        [WithAll(typeof(Projectile))]
        internal unsafe partial struct ProjectileTerrainCollisionJob : IJobEntity
        {
            [ReadOnly] public NativeTrees.NativeOctree<(Entity e, Collider c, Mask m)> tree;

            unsafe public void Execute(in LocalTransform transform, in CollidesWithTerrain collisionInfo, in Collider collider, ref DestroyAtTime projectileLifespan)
            {
                var adjustedAABB2D = collider.Add(transform);
                fixed (DestroyAtTime* lifespan_ptr = &projectileLifespan)
                {
                    var visitor = new CollisionVisitor((transform, collider), collisionInfo.Mode, lifespan_ptr);
                    tree.Range(adjustedAABB2D, ref visitor);
                }
            }

            public unsafe struct CollisionVisitor : IOctreeRangeVisitor<(Entity e, Collider c, Mask m)>
            {
                LazyCollider _collider;
                CollidesWithTerrain.eMode _objectCollisionMode;
                DestroyAtTime* _lifespanPtr;

                public CollisionVisitor(LazyCollider collider, CollidesWithTerrain.eMode objectCollisionMode, DestroyAtTime* lifespanPtr)
                {
                    _collider = collider;
                    _objectCollisionMode = objectCollisionMode;
                    _lifespanPtr = lifespanPtr;
                }

                public bool OnVisit((Entity e, Collider c, Mask m) terrain, AABB objBounds, AABB queryRange)
                {
                    if (!terrain.m.DoesInteract(_objectCollisionMode)) return true;
                    if (!terrain.c.Overlaps(_collider)) return true;
                    _lifespanPtr->DestroyTime = 0;
                    return false;
                }
            }
        }
    }
}