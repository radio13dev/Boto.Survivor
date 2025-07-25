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
    public partial struct CollectableColliderTreeSystem : ISystem
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

            m_TreeQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, CollectCollider>().WithDisabled<Collectable>().Build();
            state.RequireForUpdate(m_TreeQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var entities = m_TreeQuery.ToEntityArray(allocator: Allocator.TempJob);
            var colliders = m_TreeQuery.ToComponentDataArray<CollectCollider>(allocator: Allocator.TempJob);
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
            var a = new SurvivorCollectCollisionJob()
            {
                ecb = parallel,
                tree = m_Tree,
            }.ScheduleParallel(state.Dependency);
            
            state.Dependency = a;
        }

        public void OnDestroy(ref SystemState state)
        {
            m_Tree.Dispose();
        }
        
        public partial struct RegenerateJob : IJob
        {
            public NativeTrees.NativeOctree<Entity> tree;
            [ReadOnly] public NativeArray<Entity> entities;
            [ReadOnly] public NativeArray<CollectCollider> colliders;
            [ReadOnly] public NativeArray<LocalTransform> transforms;

            public void Execute()
            {
                tree.Clear();
                for (int i = 0; i < colliders.Length; i++)
                    tree.Insert(entities[i], colliders[i].Collider.Add(transforms[i].Position));
            }
        }

        /// <summary>
        /// Searches for overlaps between entities and their collision targets.
        /// </summary>
        [BurstCompile]
        [WithAll(typeof(SurvivorTag))]
        unsafe partial struct SurvivorCollectCollisionJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            [ReadOnly] public NativeTrees.NativeOctree<Entity> tree;

            unsafe public void Execute([ChunkIndexInQuery] int Key, Entity survivorE, in LocalTransform transform, in Collider collider)
            {
                var adjustedAABB = collider.Add(transform.Position);
                {
                    var visitor = new CollisionVisitor(Key, ref ecb, survivorE);
                    tree.Range(adjustedAABB, ref visitor);
                }
            }

            [BurstCompile]
            public unsafe struct CollisionVisitor : IOctreeRangeVisitor<Entity>
            {
                readonly int _key;
                EntityCommandBuffer.ParallelWriter _ecb;
                Entity _survivorE;

                public CollisionVisitor(int key, ref EntityCommandBuffer.ParallelWriter ecb, Entity survivorE)
                {
                    _key = key;
                    _ecb = ecb;
                    _survivorE = survivorE;
                }

                public bool OnVisit(Entity treeEntity, AABB objBounds, AABB queryRange)
                {
                    if (!objBounds.Overlaps(queryRange)) return true;

                    _ecb.SetComponent(_key, treeEntity, new Collectable(){ CollectedBy = _survivorE });
                    _ecb.SetComponentEnabled<Collectable>(_key, treeEntity, true);

                    return false;
                }
            }
        }
    }
}