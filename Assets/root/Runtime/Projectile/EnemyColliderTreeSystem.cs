using System.Threading;
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
    [BurstCompile]
    public partial struct EnemyColliderTreeSystem : ISystem
    {
        NativeTrees.NativeOctree<Entity> m_enemyTree;
        EntityQuery m_enemyQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameManager.Resources>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            m_enemyTree = new(
                new(min: new float3(-1000, -1000, -1000), max: new float3(1000, 1000, 1000)),
                Allocator.Persistent
            );

            m_enemyQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Collider>().WithAll<EnemyTag>().Build();
            //state.RequireForUpdate(m_enemyQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            // Allocate
            var enemyEntities = m_enemyQuery.ToEntityArray(allocator: Allocator.TempJob);
            var enemyColliders = m_enemyQuery.ToComponentDataArray<Collider>(allocator: Allocator.TempJob);
            var enemyTransforms = m_enemyQuery.ToComponentDataArray<LocalTransform>(allocator: Allocator.TempJob);
        
            // Update trees
            state.Dependency = new RegenerateJob()
            {
                tree = m_enemyTree,
                entities = enemyEntities,
                colliders = enemyColliders,
                transforms = enemyTransforms 
            }.Schedule(state.Dependency);
            enemyEntities.Dispose(state.Dependency);
            enemyColliders.Dispose(state.Dependency);
            enemyTransforms.Dispose(state.Dependency);
            
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
            m_enemyTree.Dispose();
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
            [ReadOnly] public NativeTrees.NativeOctree<Entity> tree;
            [ReadOnly] public double time;

            unsafe public void Execute([EntityIndexInChunk] int Key, Entity entity, in LocalTransform transform, in Collider collider, in Movement movement, ref LaserProjectileSpawner laserSpawner)
            {
                if (laserSpawner.LastProjectileTime + laserSpawner.TimeBetweenShots > time) return;
            
                fixed (FireAtNearestTargetJob* job_ptr = &this)
                fixed (LaserProjectileSpawner* spawner_ptr = &laserSpawner)
                {
                    var visitor = new NearestVisitor(Key, job_ptr, transform, movement, spawner_ptr);
                    var distance = new DistanceProvider();

                    tree.Nearest(transform.Position, 30, ref visitor, distance);
                    var dir = laserSpawner.LastProjectileDirection;
                    
                    var laser = ecb.Instantiate(Key, resources.Projectile_Survivor_Laser);
                    var laserT = transform;
                    laserT.Rotation = quaternion.LookRotationSafe(TorusMapper.ToroidalToCartesian(dir.x, dir.y), laserT.Up());
                    ecb.AddComponent<SurvivorProjectileTag>(Key, laser);
                    ecb.SetComponent(Key, laser, laserT);
                    ecb.SetComponent(Key, laser, new SurfaceMovement(){ Velocity = new float2(8,0)});
                    ecb.SetComponent(Key, laser, new DestroyAtTime(){ DestroyTime = time + laserSpawner.Lifespan });
                    
                    laserSpawner.LastProjectileTime = time;
                }
            }

            [BurstCompile]
            unsafe struct NearestVisitor : IOctreeNearestVisitor<Entity>
            {
                public volatile int Hits;
                int _key;
                FireAtNearestTargetJob* _job;
                LocalTransform _transform;
                Movement _sourceMovement;
                LaserProjectileSpawner* _sourceSpawner;

                public NearestVisitor(int Key, FireAtNearestTargetJob* job, LocalTransform transform, Movement sourceMovement, LaserProjectileSpawner* sourceSpawner)
                {
                    Hits = 0;
                    
                    _key = Key;
                    _job = job;
                    _transform = transform;
                    _sourceMovement = sourceMovement;
                    _sourceSpawner = sourceSpawner;
                }

                public bool OnVist(Entity obj, AABB bounds)
                {
                    Interlocked.Increment(ref Hits);
                    TorusMapper.CartesianToToroidal(_transform.Position, out var myTheta, out var myPhi, out _);
                    TorusMapper.CartesianToToroidal(bounds.Center, out var targetTheta, out var targetPhi, out _);
                    var dirToroidal = math.normalizesafe(new float2(targetTheta - myTheta, targetPhi - myPhi), new float2(1,0));
                    _sourceSpawner->LastProjectileDirection = dirToroidal;
                    return false;
                }
            }

            [BurstCompile]
            unsafe struct DistanceProvider : IOctreeDistanceProvider<Entity>
            {
                public float DistanceSquared(float3 point, Entity obj, AABB bounds)
                {
                    return math.distancesq(point, bounds.Center);
                }
            }
        }

        [BurstCompile]
        unsafe partial struct EnemyPushForceJob : IJobEntity
        {
            [ReadOnly] public NativeTrees.NativeOctree<Entity> tree;

            unsafe public void Execute([EntityIndexInChunk] int Key, Entity entity, in LocalTransform transform, in Collider collider, ref Force force)
            {
                var adjustedAABB2D = collider.Add(transform.Position);
                fixed (Force* force_ptr = &force)
                {
                    var visitor = new CollisionVisitor(entity, force_ptr);
                    tree.Range(adjustedAABB2D, ref visitor);
                }
            }


            public unsafe struct CollisionVisitor : IOctreeRangeVisitor<Entity>
            {
                Entity _source;
                Force* _force;

                public CollisionVisitor(Entity source, Force* force)
                {
                    _source = source;
                    _force = force;
                }

                public bool OnVisit(Entity projectile, AABB objBounds, AABB queryRange)
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