using NativeTrees;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

/// <summary>
/// Projectile collision is predicted, only after all movement is done
/// </summary>
[UpdateAfter(typeof(MovementSystemGroup))]
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
public partial class ProjectileCollisionSystemGroup : ComponentSystemGroup
{
        
}

[GhostComponent]
public struct Collider : IComponentData, IEnableableComponent
{
    public AABB2D Value;
    public int Category;
    public int Index;
    
    public Collider(AABB2D value)
    {
        Value = value;
        Category = 0;
        Index = -1;
    }
}

[UpdateInGroup(typeof(ProjectileCollisionSystemGroup))]
public partial struct ProjectileCollisionSystem : ISystem
{
    EntityQuery m_toRegister;
    
    NativeQuadtree<Entity> survivorTree;
    NativeQuadtree<Entity> survivorProjectileTree;
    NativeQuadtree<Entity> enemyTree;
    NativeQuadtree<Entity> enemyProjectileTree;

    public void OnCreate(ref SystemState state)
    {
        m_toRegister = SystemAPI.QueryBuilder().WithDisabled<Collider>().Build();
        
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        survivorTree = new NativeQuadtree<Entity>(
            new AABB2D(new float2(-1000, -1000), 
                new float2(1000, 1000)), 
            Allocator.Persistent);
        survivorProjectileTree = new NativeQuadtree<Entity>(
            new AABB2D(new float2(-1000, -1000), 
                new float2(1000, 1000)), 
            Allocator.Persistent);
        enemyTree = new NativeQuadtree<Entity>(
            new AABB2D(new float2(-1000, -1000), 
                new float2(1000, 1000)), 
            Allocator.Persistent);
        enemyProjectileTree = new NativeQuadtree<Entity>(
            new AABB2D(new float2(-1000, -1000), 
                new float2(1000, 1000)), 
            Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        survivorTree.Dispose();
        survivorProjectileTree.Dispose();
        enemyTree.Dispose();
        enemyProjectileTree.Dispose();
    }


    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        new CollisionJob()
        {
            ecb = delayedEcb.AsParallelWriter(),
            projectiles = enemyProjectileTree
        }.Schedule();
        
        //var entities = m_toRegister.ToEntityArray(Allocator.Temp);
        //if (entities.Length > 0)
        //{
        //    var colliders = m_toRegister.ToComponentDataArray<Collider>(Allocator.Temp);
        //    for (int i = 0; i < entities.Length; i++)
        //    {
        //        SystemAPI.SetComponentEnabled<Collider>(entities[i], true);
        //        switch (colliders[i].Index)
        //        {
        //        
        //        }
        //    }
        //}
    }
    
    /// <summary>
    /// Searches for overlaps between entities and their collision targets.
    /// </summary>
    [WithNone(typeof(EnableRaycastCollisionDetect))]
    [WithAll(typeof(CharacterTag))]
    [WithAll(typeof(SurvivorTag))]
    partial struct CollisionJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public NativeQuadtree<Entity> projectiles;
        
        public void Execute(Entity entity, in LocalTransform transform, in Collider collider, ref DynamicBuffer<ProjectileCharacterCollision> collisions)
        {
            var xy = transform.Position.xy;
            var aabb = new AABB2D(collider.Value.min + xy, collider.Value.max + xy);
            var visitor = new CollisionVisitor(ref ecb, ref collisions);
            projectiles.Range(aabb, ref visitor);
        }
        
        public partial struct CollisionVisitor : IQuadtreeRangeVisitor<Entity>
        {
            EntityCommandBuffer.ParallelWriter _ecb;
            DynamicBuffer<ProjectileCharacterCollision> _collisionsBuffer;
            NativeParallelHashSet<Entity> _ignoredCollisions;
            
            public CollisionVisitor(ref EntityCommandBuffer.ParallelWriter ecb, ref DynamicBuffer<ProjectileCharacterCollision> collisionsBuffer)
            {
                _ecb = ecb;
                _collisionsBuffer = collisionsBuffer;
                _ignoredCollisions = new NativeParallelHashSet<Entity>(4 + collisionsBuffer.Length, Allocator.TempJob);
                if (_collisionsBuffer.Length > 0)
                    for (int i = 0; i < _collisionsBuffer.Length; i++)
                        _ignoredCollisions.Add(_collisionsBuffer[i].Projectile);
            }
        
            public bool OnVisit(Entity projectile, AABB2D objBounds, AABB2D queryRange)
            {
                if (_ignoredCollisions.Add(projectile))
                {
                    _ecb.DestroyEntity(0, projectile);
                    _collisionsBuffer.Add(new ProjectileCharacterCollision(projectile));
                }
                return true;
            }
        }
    }
}