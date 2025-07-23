/*
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
    [UpdateInGroup(typeof(CollisionSystemGroup))]
    public partial struct PlayerColliderTreeSystem : ISystem
    {
        NativeTrees.NativeOctree<Entity> m_Tree;
        EntityQuery m_TreeQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            m_Tree = new(
                new(min: new float3(-1000, -1000, -1000), max: new float3(1000, 1000, 1000)),
                Allocator.Persistent
            );

            m_TreeQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Collider>().WithAll<SurvivorTag>().Build();
            state.RequireForUpdate(m_TreeQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
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

            // Perform collisions
            var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var parallel = delayedEcb.AsParallelWriter();
            state.Dependency = new CollectableCollisionJob()
            {
                ecb = parallel,
                tree = m_Tree,
            }.ScheduleParallel(state.Dependency);
        }

        public void OnDestroy(ref SystemState state)
        {
            m_Tree.Dispose();
        }

        /// <summary>
        /// Searches for overlaps between entities and their collision targets.
        /// </summary>
        [BurstCompile]
        [WithPresent(typeof(Collectable))]
        unsafe partial struct CollectableCollisionJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            [ReadOnly] public NativeTrees.NativeOctree<Entity> tree;

            unsafe public void Execute([ChunkIndexInQuery] int Key, Entity collectableE, in LocalTransform transform, in Collider collider)
            {
                var adjustedAABB = collider.Add(transform.Position);
                {
                    var visitor = new CollisionVisitor(Key, ref ecb, collectableE);
                    tree.Range(adjustedAABB, ref visitor);
                }
            }

            [BurstCompile]
            public unsafe struct CollisionVisitor : IOctreeRangeVisitor<Entity>
            {
                readonly int _key;
                EntityCommandBuffer.ParallelWriter _ecb;
                Entity _collectableE;

                public CollisionVisitor(int key, ref EntityCommandBuffer.ParallelWriter ecb, Entity collectableE)
                {
                    _key = key;
                    _ecb = ecb;
                    _collectableE = collectableE;
                }

                public bool OnVisit(Entity treeEntity, AABB objBounds, AABB queryRange)
                {
                    if (!objBounds.Overlaps(queryRange)) return true;

                    _ecb.SetComponent(_key, _collectableE, new Collectable(){ CollectedBy = treeEntity });
                    _ecb.SetComponentEnabled<Collectable>(_key, _collectableE, true);

                    return false;
                }
            }
        }
    }
}
*/