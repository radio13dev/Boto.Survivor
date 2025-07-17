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

            m_enemyQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Collider>().WithAll<EnemyTag>().Build();
            //state.RequireForUpdate(m_enemyQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            // Update trees
            var enemy_entities = m_enemyQuery.ToEntityArray(allocator: Allocator.TempJob);
            var enemy_colliders = m_enemyQuery.ToComponentDataArray<Collider>(allocator: Allocator.TempJob);
            var enemy_transforms = m_enemyQuery.ToComponentDataArray<LocalTransform>(allocator: Allocator.TempJob);
            state.Dependency = new RegenerateJob()
            {
                tree = m_enemyTree,
                entities = enemy_entities,
                colliders = enemy_colliders,
                transforms = enemy_transforms
            }.Schedule(state.Dependency);

            // Wait for that
            state.CompleteDependency();

            // Perform collisions
            var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var parallel = delayedEcb.AsParallelWriter();
            new FireAtNearestTargetJob()
            {
                ecb = parallel,
                resources = SystemAPI.GetSingleton<GameManager.Resources>(),
                tree = m_enemyTree,
                time = SystemAPI.Time.ElapsedTime
            }.Schedule();

            SystemAPI.QueryBuilder().Build().CalculateEntityCount();

            enemy_entities.Dispose();
            enemy_colliders.Dispose();
            enemy_transforms.Dispose();
        }

        [BurstCompile]
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
        [WithAll(typeof(LaserProjectileSpawner), typeof(SurvivorTag))]
        [BurstCompile]
        unsafe partial struct FireAtNearestTargetJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            [ReadOnly] public GameManager.Resources resources;
            [ReadOnly] public NativeTrees.NativeQuadtree<Entity> tree;
            [ReadOnly] public double time;

            unsafe public void Execute([EntityIndexInChunk] int Key, Entity entity, in LocalTransform transform, in Collider collider, in Movement movement, ref LaserProjectileSpawner laserSpawner)
            {
                if (laserSpawner.LastProjectileTime + laserSpawner.TimeBetweenShots > time) return;
            
                fixed (FireAtNearestTargetJob* job_ptr = &this)
                fixed (LaserProjectileSpawner* spawner_ptr = &laserSpawner)
                {
                    var visitor = new NearestVisitor(Key, job_ptr, transform, movement, spawner_ptr);
                    var distance = new DistanceProvider();

                    tree.Nearest(transform.Position.xy, 30, ref visitor, distance);
                    
                    float2 dir;
                    if (visitor.Hits == 0)
                        dir = movement.LastDirection;
                    else
                        dir = math.normalizesafe(laserSpawner.LastProjectileDirection, movement.LastDirection);
                    dir = math.normalizesafe(dir, new float2(1,0));
                    
                    var laser = ecb.Instantiate(Key, resources.Projectile_Survivor_Laser);
                    var laserT = transform.RotateZ(math.atan2(dir.y, dir.x));
                    ecb.AddComponent<SurvivorProjectileTag>(Key, laser);
                    ecb.SetComponent(Key, laser, laserT);
                    ecb.SetComponent(Key, laser, new Movement(dir));
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

                public bool OnVist(Entity obj, AABB2D bounds)
                {
                    Interlocked.Increment(ref Hits);
                    var dir = bounds.Center - _transform.Position.xy;
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
    }
}