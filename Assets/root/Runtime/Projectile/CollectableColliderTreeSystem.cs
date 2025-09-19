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
        NativeTrees.NativeOctree<(Entity, Collectable)> m_Tree;
        EntityQuery m_TreeQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
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
            var collectable = m_TreeQuery.ToComponentDataArray<Collectable>(allocator: Allocator.TempJob);
            state.Dependency = new RegenerateJob()
            {
                tree = m_Tree,
                entities = entities,
                colliders = colliders,
                transforms = transforms,
                collectables = collectable
            }.Schedule(state.Dependency);
            entities.Dispose(state.Dependency);
            colliders.Dispose(state.Dependency);
            transforms.Dispose(state.Dependency);
            collectable.Dispose(state.Dependency);

            // Perform collisions
            var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
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
            public NativeTrees.NativeOctree<(Entity, Collectable)> tree;
            [ReadOnly] public NativeArray<Entity> entities;
            [ReadOnly] public NativeArray<CollectCollider> colliders;
            [ReadOnly] public NativeArray<LocalTransform> transforms;
            [ReadOnly] public NativeArray<Collectable> collectables;

            public void Execute()
            {
                tree.Clear();
                for (int i = 0; i < colliders.Length; i++)
                    tree.Insert((entities[i], collectables[i]), colliders[i].Collider.Add(transforms[i]));
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
            [ReadOnly] public NativeTrees.NativeOctree<(Entity, Collectable)> tree;

            unsafe public void Execute([ChunkIndexInQuery] int Key, Entity survivorE, in PlayerControlled playerControlled, in LocalTransform transform, in Collider collider)
            {
                var adjustedAABB = collider.Add(transform);
                {
                    var visitor = new CollisionVisitor(Key, ref ecb, playerControlled);
                    tree.Range(adjustedAABB, ref visitor);
                }
            }

            [BurstCompile]
            public unsafe struct CollisionVisitor : IOctreeRangeVisitor<(Entity, Collectable)>
            {
                readonly int _key;
                EntityCommandBuffer.ParallelWriter _ecb;
                PlayerControlled _player;

                public CollisionVisitor(int key, ref EntityCommandBuffer.ParallelWriter ecb, PlayerControlled player)
                {
                    _key = key;
                    _ecb = ecb;
                    _player = player;
                }

                public bool OnVisit((Entity, Collectable) treeEntity, AABB objBounds, AABB queryRange)
                {
                    if (treeEntity.Item2.PlayerId != _player.Index) return true;

                    _ecb.SetComponentEnabled<Collectable>(_key, treeEntity.Item1, true);

                    return true;
                }
            }
        }
    }
}