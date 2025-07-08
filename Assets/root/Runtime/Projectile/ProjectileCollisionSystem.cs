using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Collisions
{
    /// <summary>
    /// Projectile collision is predicted, only after all movement is done
    /// </summary>
    [UpdateAfter(typeof(MovementSystemGroup))]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial class ProjectileCollisionSystemGroup : ComponentSystemGroup
    {
    }

    [GhostComponent]
    [BurstCompile]
    public struct Collider : IComponentData
    {
        public NativeTrees.AABB2D Value;

        public Collider(AABB2D value)
        {
            Value = value;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public NativeTrees.AABB2D Add(float2 offset)
        {
            return new NativeTrees.AABB2D(Value.min + offset, Value.max + offset);
        }
    }

    public struct Survivor : IComponentData
    {
        public const int Index = 0;
    }

    public struct SurvivorProjectile : IComponentData
    {
        public const int Index = 1;
    }

    public struct Enemy : IComponentData
    {
        public const int Index = 2;
    }

    public struct EnemyProjectile : IComponentData
    {
        public const int Index = 3;
    }

    [UpdateInGroup(typeof(ProjectileCollisionSystemGroup))]
    public partial class ProjectileCollisionSystem : SystemBase
    {
        NativeArray<(NativeTrees.NativeQuadtree<Entity> Tree, EntityQuery Query)> m_trees;

        NativeTrees.NativeQuadtree<Entity> SurvivorTree => m_trees[0].Tree;
        NativeTrees.NativeQuadtree<Entity> SurvivorProjectileTree => m_trees[1].Tree;
        NativeTrees.NativeQuadtree<Entity> EnemyTree => m_trees[2].Tree;
        NativeTrees.NativeQuadtree<Entity> EnemyProjectileTree => m_trees[3].Tree;

        EntityQuery SurvivorQuery => m_trees[0].Query;
        EntityQuery SurvivorProjectile => m_trees[1].Query;
        EntityQuery EnemyQuery => m_trees[2].Query;
        EntityQuery EnemyProjectileQuery => m_trees[3].Query;

        protected override void OnCreate()
        {
            base.OnCreate();

            RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();

            m_trees = new NativeArray<(NativeQuadtree<Entity> Tree, EntityQuery Query)>(4, Allocator.Persistent);
            m_trees[0] = (
                new(
                    new(min: new float2(-1000, -1000), max: new float2(1000, 1000)),
                    Allocator.Persistent
                ),
                SystemAPI.QueryBuilder().WithAll<LocalTransform, Collider>().WithAll<Survivor, Health>().Build()
            );
            m_trees[1] = (
                new(
                    new(min: new float2(-1000, -1000), max: new float2(1000, 1000)),
                    Allocator.Persistent
                ),
                SystemAPI.QueryBuilder().WithAll<LocalTransform, Collider>().WithAll<SurvivorProjectile>().Build()
            );
            m_trees[2] = (
                new(
                    new(min: new float2(-1000, -1000), max: new float2(1000, 1000)),
                    Allocator.Persistent
                ),
                SystemAPI.QueryBuilder().WithAll<LocalTransform, Collider>().WithAll<Enemy, Health>().Build()
            );
            m_trees[3] = (
                new(
                    new(min: new float2(-1000, -1000), max: new float2(1000, 1000)),
                    Allocator.Persistent
                ),
                SystemAPI.QueryBuilder().WithAll<LocalTransform, Collider>().WithAll<EnemyProjectile>().Build()
            );
            
            for (int i = 0; i < m_trees.Length; i++)
                RequireAnyForUpdate(m_trees[i].Query);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            for (int i = 0; i < m_trees.Length; i++)
            {
                m_trees[i].Tree.Dispose();
            }

            m_trees.Dispose();
        }

        protected override void OnUpdate()
        {
            // Update trees
            RegenerateJob[] jobs = new RegenerateJob[m_trees.Length];
            for (int i = 0; i < m_trees.Length; i++)
            {
                jobs[i] = new RegenerateJob()
                {
                    tree = m_trees[i].Tree,
                    entities = m_trees[i].Query.ToEntityArray(allocator: Allocator.TempJob),
                    colliders = m_trees[i].Query.ToComponentDataArray<Collider>(allocator: Allocator.TempJob),
                    transforms = m_trees[i].Query.ToComponentDataArray<LocalTransform>(allocator: Allocator.TempJob),
                };
                this.Dependency = jobs[i].Schedule(this.Dependency);
            }

            // Wait for that
            this.CompleteDependency();

            for (int i = 0; i < jobs.Length; i++)
            {
                jobs[i].entities.Dispose();
                jobs[i].colliders.Dispose();
                jobs[i].transforms.Dispose();
            }

            // Perform collisions
            var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            var parallel = delayedEcb.AsParallelWriter();

            // ~~ Survivor Hit Checks ~~
            this.Dependency = new CollisionJob()
            {
                ecb = parallel,
                tree = EnemyProjectileTree
            }.Schedule(SurvivorQuery, this.Dependency);

            // ~~ Enemy Hit Checks ~~
            this.Dependency = new CollisionJob()
            {
                ecb = parallel,
                tree = SurvivorProjectileTree
            }.Schedule(EnemyQuery, this.Dependency);

            this.CompleteDependency();
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

            unsafe public void Execute([ChunkIndexInQuery] int Key, Entity entity, in LocalTransform transform, in Collider collider, ref Health health)
            {
                var adjustedAABB2D = collider.Add(transform.Position.xy);
                fixed (Health* ptr = &health)
                {
                    var visitor = new CollisionVisitor(Key, ref ecb, ptr);
                    tree.Range(adjustedAABB2D, ref visitor);
                    visitor.Dispose();
                }
            }

            public unsafe struct CollisionVisitor : IQuadtreeRangeVisitor<Entity>
            {
                readonly int _key;
                EntityCommandBuffer.ParallelWriter _ecb;
                Health* _health;
                NativeParallelHashSet<Entity> _ignoredCollisions;

                public CollisionVisitor(int key, ref EntityCommandBuffer.ParallelWriter ecb, Health* health)
                {
                    _key = key;
                    _ecb = ecb;
                    _health = health;
                    _ignoredCollisions = new NativeParallelHashSet<Entity>(4, Allocator.TempJob);
                }

                public bool OnVisit(Entity projectile, AABB2D objBounds, AABB2D queryRange)
                {
                    if (!objBounds.Overlaps(queryRange)) return true;

                    if (_ignoredCollisions.Add(projectile))
                    {
                        // Destroy projectiles when they collide (by setting their life to 0)
                        _ecb.SetComponent(_key, projectile, new Projectile());
                        _health->Value -= 1;
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