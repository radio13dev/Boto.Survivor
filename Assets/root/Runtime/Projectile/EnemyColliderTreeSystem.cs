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

            // Perform collisions
            var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var parallel = delayedEcb.AsParallelWriter();
            var a = new FireAtNearestTargetJob()
            {
                ecb = parallel,
                resources = SystemAPI.GetSingleton<GameManager.Resources>(),
                tree = m_enemyTree,
                time = SystemAPI.Time.ElapsedTime
            }.ScheduleParallel(state.Dependency);
            
            var b = new EnemyPushForceJob()
            {
                tree = m_enemyTree
            }.ScheduleParallel(state.Dependency);
            
            var c = new ProjectileHitEnemyJob()
            {
                tree = m_enemyTree
            }.ScheduleParallel(state.Dependency);
            
            state.Dependency = JobHandle.CombineDependencies(a,b,c);
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
                    var visitor = new NearestVisitor(Key, job_ptr, transform, spawner_ptr);
                    var distance = new DistanceProvider();

                    tree.Nearest(transform.Position, 30, ref visitor, distance);
                    
                    if (visitor.Hits == 0)
                    {
                        laserSpawner.LastProjectileDirection = movement.LastDirection;
                    }
                    
                    var laser = ecb.Instantiate(Key, resources.Projectile_Survivor_Laser);
                    var laserT = transform;
                    laserT.Rotation = math.mul(quaternion.AxisAngle(transform.Up(), -math.PIHALF), quaternion.LookRotationSafe(laserSpawner.LastProjectileDirection, transform.Up()));
                    ecb.SetComponent(Key, laser, laserT);
                    ecb.SetComponent(Key, laser, new SurfaceMovement(){ Velocity = new float2(8,0)});
                    ecb.SetComponent(Key, laser, new DestroyAtTime(){ DestroyTime = time + laserSpawner.Lifespan });
                    
                    laserSpawner.LastProjectileTime = time;
                }
            }

            [BurstCompile]
            unsafe struct NearestVisitor : IOctreeNearestVisitor<Entity>
            {
                public int Hits;
                int _key;
                FireAtNearestTargetJob* _job;
                LocalTransform _transform;
                LaserProjectileSpawner* _sourceSpawner;

                public NearestVisitor(int Key, FireAtNearestTargetJob* job, LocalTransform transform, LaserProjectileSpawner* sourceSpawner)
                {
                    Hits = 0;
                    
                    _key = Key;
                    _job = job;
                    _transform = transform;
                    _sourceSpawner = sourceSpawner;
                }

                public bool OnVist(Entity obj, AABB bounds)
                {
                    Interlocked.Increment(ref Hits);
                    _sourceSpawner->LastProjectileDirection = bounds.Center - _transform.Position;
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
                }
            }
        }
        
        [BurstCompile]
        [WithAll(typeof(SurvivorProjectileTag))]
        [WithDisabled(typeof(ProjectileHit))]
        unsafe partial struct ProjectileHitEnemyJob : IJobEntity
        {
            [ReadOnly] public NativeTrees.NativeOctree<Entity> tree;

            public unsafe void Execute([EntityIndexInChunk] int Key, Entity projectileE, in LocalTransform transform, in Collider collider, 
                ref ProjectileHit projectileHit, EnabledRefRW<ProjectileHit> projectileHitState)
            {
                var adjustedAABB2D = collider.Add(transform.Position);
                fixed (ProjectileHit* projectileHit_ptr = &projectileHit)
                {
                    var visitor = new CollisionVisitor(projectileE, projectileHit_ptr, projectileHitState);
                    tree.Range(adjustedAABB2D, ref visitor);
                }
            }

            public unsafe struct CollisionVisitor : IOctreeRangeVisitor<Entity>
            {
                Entity _projectileE;
                ProjectileHit* _projectileHit_ptr;
                EnabledRefRW<ProjectileHit> _projectileHitState;

                public CollisionVisitor(Entity projectileE, ProjectileHit* projectileHit_ptr, EnabledRefRW<ProjectileHit> projectileHitState)
                {
                    _projectileE = projectileE;
                    _projectileHit_ptr = projectileHit_ptr;
                    _projectileHitState = projectileHitState;
                }

                public bool OnVisit(Entity enemyE, AABB objBounds, AABB queryRange)
                {
                    if (!objBounds.Overlaps(queryRange)) return true;
                    _projectileHit_ptr->HitEntity = enemyE;
                    _projectileHitState.ValueRW = true;
                    return true;
                }
            }
        }
    }
}