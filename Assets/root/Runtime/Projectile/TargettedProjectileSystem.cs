using System.Threading;
using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Collisions
{
    [UpdateInGroup(typeof(CollisionSystemGroup))]
    [BurstCompile]
    public partial struct EnemyColliderTreeSystem : ISystem
    {
        NativeTrees.NativeQuadtree<Entity> m_enemyTree;
        EntityQuery m_enemyQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameManager.Resources>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            m_enemyTree = new(
                new(min: new float2(-1000, -1000), max: new float2(1000, 1000)),
                Allocator.Persistent
            );

            m_enemyQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform2D, Collider>().WithAll<EnemyTag>().Build();
            //state.RequireForUpdate(m_enemyQuery);
        }

        NativeArray<Entity> m_EnemyQueryEntities;
        NativeArray<Collider> m_EnemyQueryColliders;
        NativeArray<LocalTransform2D> m_EnemyQueryTransforms;
        
        public void OnUpdate(ref SystemState state)
        {
            m_EnemyQueryEntities.Dispose();
            m_EnemyQueryColliders.Dispose();
            m_EnemyQueryTransforms.Dispose();
            
            // Update trees
            state.Dependency = new RegenerateJob()
            {
                tree = m_enemyTree,
                entities = m_EnemyQueryEntities = m_enemyQuery.ToEntityArray(allocator: Allocator.TempJob),
                colliders = m_EnemyQueryColliders = m_enemyQuery.ToComponentDataArray<Collider>(allocator: Allocator.TempJob),
                transforms = m_EnemyQueryTransforms = m_enemyQuery.ToComponentDataArray<LocalTransform2D>(allocator: Allocator.TempJob)
            }.Schedule(state.Dependency);
            
            state.CompleteDependency();

            // Perform collisions
            var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var parallel = delayedEcb.AsParallelWriter();
            state.Dependency = new FireAtNearestTargetJob()
            {
                ecb = parallel,
                resources = SystemAPI.GetSingleton<GameManager.Resources>(),
                tree = m_enemyTree,
                time = SystemAPI.Time.ElapsedTime
            }.Schedule(state.Dependency);
            
            state.Dependency = new EnemyPushForceJob()
            {
                tree = m_enemyTree
            }.Schedule(state.Dependency);
        }

        public void OnDestroy(ref SystemState state)
        {
            m_EnemyQueryEntities.Dispose();
            m_EnemyQueryColliders.Dispose();
            m_EnemyQueryTransforms.Dispose();
            m_enemyTree.Dispose();
        }

        [BurstCompile]
        partial struct RegenerateJob : IJob
        {
            public NativeTrees.NativeQuadtree<Entity> tree;
            [ReadOnly] public NativeArray<Entity> entities;
            [ReadOnly] public NativeArray<Collider> colliders;
            [ReadOnly] public NativeArray<LocalTransform2D> transforms;

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
        [WithAll(typeof(LaserProjectileSpawner), typeof(SurvivorTag))]
        [BurstCompile]
        unsafe partial struct FireAtNearestTargetJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            [ReadOnly] public GameManager.Resources resources;
            [ReadOnly] public NativeTrees.NativeQuadtree<Entity> tree;
            [ReadOnly] public double time;

            unsafe public void Execute([EntityIndexInChunk] int Key, Entity entity, in LocalTransform2D transform, in Collider collider, in Movement movement, ref LaserProjectileSpawner laserSpawner)
            {
                if (laserSpawner.LastProjectileTime + laserSpawner.TimeBetweenShots > time) return;
            
                fixed (FireAtNearestTargetJob* job_ptr = &this)
                fixed (LaserProjectileSpawner* spawner_ptr = &laserSpawner)
                {
                    var visitor = new NearestVisitor(Key, job_ptr, transform, movement, spawner_ptr);
                    var distance = new DistanceProvider();

                    tree.Nearest(transform.Position, 30, ref visitor, distance);
                    
                    float2 dir;
                    if (visitor.Hits == 0)
                        dir = movement.LastDirection;
                    else
                        dir = math.normalizesafe(laserSpawner.LastProjectileDirection, movement.LastDirection);
                    dir = math.normalizesafe(dir, new float2(1,0));
                    
                    var laser = ecb.Instantiate(Key, resources.Projectile_Survivor_Laser);
                    var laserT = transform;
                    laserT.Rotation = math.atan2(dir.y, dir.x);
                    ecb.AddComponent<SurvivorProjectileTag>(Key, laser);
                    ecb.SetComponent(Key, laser, laserT);
                    ecb.SetComponent(Key, laser, new Movement(dir*8));
                    ecb.SetComponent(Key, laser, new DestroyAtTime(){ DestroyTime = time + laserSpawner.Lifespan });
                    
                    laserSpawner.LastProjectileTime = time;
                }
            }

            [BurstCompile]
            unsafe struct NearestVisitor : IQuadtreeNearestVisitor<Entity>
            {
                public volatile int Hits;
                int _key;
                FireAtNearestTargetJob* _job;
                LocalTransform2D _transform;
                Movement _sourceMovement;
                LaserProjectileSpawner* _sourceSpawner;

                public NearestVisitor(int Key, FireAtNearestTargetJob* job, LocalTransform2D transform, Movement sourceMovement, LaserProjectileSpawner* sourceSpawner)
                {
                    Hits = 0;
                    
                    _key = Key;
                    _job = job;
                    _transform = transform;
                    _sourceMovement = sourceMovement;
                    _sourceSpawner = sourceSpawner;
                }

                public bool OnVist(Entity obj, AABB2D bounds)
                {
                    Interlocked.Increment(ref Hits);
                    var dir = bounds.Center - _transform.Position;
                    _sourceSpawner->LastProjectileDirection = dir;
                    return false;
                }
            }

            [BurstCompile]
            unsafe struct DistanceProvider : IQuadtreeDistanceProvider<Entity>
            {
                public float DistanceSquared(float2 point, Entity obj, AABB2D bounds)
                {
                    return math.distancesq(point, bounds.Center);
                }
            }
        }

        [BurstCompile]
        unsafe partial struct EnemyPushForceJob : IJobEntity
        {
            [ReadOnly] public NativeTrees.NativeQuadtree<Entity> tree;

            unsafe public void Execute([EntityIndexInChunk] int Key, Entity entity, in LocalTransform2D transform, in Collider collider, ref Force force)
            {
                var adjustedAABB2D = collider.Add(transform.Position.xy);
                fixed (Force* force_ptr = &force)
                {
                    var visitor = new CollisionVisitor(entity, force_ptr);
                    tree.Range(adjustedAABB2D, ref visitor);
                }
            }


            public unsafe struct CollisionVisitor : IQuadtreeRangeVisitor<Entity>
            {
                Entity _source;
                Force* _force;

                public CollisionVisitor(Entity source, Force* force)
                {
                    _source = source;
                    _force = force;
                }

                public bool OnVisit(Entity projectile, AABB2D objBounds, AABB2D queryRange)
                {
                    if (_source == projectile) return true;
                    if (!objBounds.Overlaps(queryRange)) return true;
                    
                    _force->Velocity += (queryRange.Center - objBounds.Center)/2;
                    return true;
                    
                    /*
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
                    */
                }
            }
        }
    }
}