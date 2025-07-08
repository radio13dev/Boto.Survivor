using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using NativeQuadTree;
using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using AABB2D = NativeQuadTree.AABB2D;

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
public struct Collider : IComponentData, IEnableableComponent
{
    public AABB2D Value;
    public Mask Category;
    public int Index;

    [Flags]
    public enum Mask
    {
        Survivor,
        Enemy,
        Projectile
    }

    public Collider(AABB2D value)
    {
        Value = value;
        Category = 0;
        Index = -1;
    }
    
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public AABB2D Add(float2 offset)
    {
        return new AABB2D(Value.Center + offset, Value.Extents);
    }
    
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public QuadElement<Entity> ToQuadElement(Entity entity)
    {
        return new QuadElement<Entity>(){ element = entity, pos = Value.Center };
    }
    
}

[UpdateInGroup(typeof(ProjectileCollisionSystemGroup))]
public partial struct CollisionSetupSystem : ISystem
{
    ProjectileCollisionSystem m_projectileCollisionSystem;
    EntityQuery m_toRegister;

    public void OnCreate(ref SystemState state)
    {
        m_toRegister = SystemAPI.QueryBuilder().WithAll<LocalTransform>().WithDisabled<Collider>().Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (m_projectileCollisionSystem == null)
        {
            m_projectileCollisionSystem = SystemAPI.ManagedAPI.GetSingleton<ProjectileCollisionSystem>();
            if (m_projectileCollisionSystem == null) return;
        }

        var entities = m_toRegister.ToEntityArray(Allocator.Temp);
        if (entities.Length > 0)
        {
            var colliders = m_toRegister.ToComponentDataArray<Collider>(Allocator.Temp);
            var transforms = m_toRegister.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                m_projectileCollisionSystem.AddToQuad(entities[i], transforms[i], colliders[i]);
                SystemAPI.SetComponentEnabled<Collider>(entities[i], true);
            }

            transforms.Dispose();
            colliders.Dispose();
        }

        entities.Dispose();
    }
}

[UpdateInGroup(typeof(ProjectileCollisionSystemGroup))]
public partial class ProjectileCollisionSystem : SystemBase
{
    EntityQuery m_toRegister;

    NativeQuadTree<Entity> survivorTree;
    NativeQuadTree<Entity> survivorProjectileTree;
    NativeQuadTree<Entity> enemyTree;
    NativeQuadTree<Entity> enemyProjectileTree;

    protected override void OnCreate()
    {
        base.OnCreate();

        RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();

        m_toRegister = SystemAPI.QueryBuilder().WithDisabled<Collider>().Build();

        survivorTree = new NativeQuadTree<Entity>(new AABB2D(){ Center = new float2(0,0), Extents = new float2(1000, 1000)}, Allocator.Persistent);
        survivorProjectileTree = new NativeQuadTree<Entity>(new AABB2D(){ Center = new float2(0,0), Extents = new float2(1000, 1000)}, Allocator.Persistent);
        enemyTree = new NativeQuadTree<Entity>(new AABB2D(){ Center = new float2(0,0), Extents = new float2(1000, 1000)}, Allocator.Persistent);
        enemyProjectileTree = new NativeQuadTree<Entity>(new AABB2D(){ Center = new float2(0,0), Extents = new float2(1000, 1000)}, Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        survivorTree.Dispose();
        survivorProjectileTree.Dispose();
        enemyTree.Dispose();
        enemyProjectileTree.Dispose();
    }

    public void AddToQuad(in Entity entity, in LocalTransform entityT, in Collider collider)
    {
        var aabb2d = collider.Add(entityT.Position.xy);
        if (collider.Category == (Collider.Mask.Survivor | Collider.Mask.Projectile))
        {
            survivorProjectileTree.Insert(entity, aabb2d);
        }
        else if (collider.Category == Collider.Mask.Survivor)
        {
            survivorTree.Insert(entity, aabb2d);
        }
        else if (collider.Category == (Collider.Mask.Enemy | Collider.Mask.Projectile))
        {
            enemyProjectileTree.Insert(entity, aabb2d);
        }
        else if (collider.Category == Collider.Mask.Enemy)
        {
            enemyTree.Insert(entity, aabb2d);
        }
        else
        {
            Debug.Log($"{entity.ToFixedString()} didn't have a valid Collider Mask: {collider.Category}");
        }
    }

    protected override void OnUpdate()
    {
        // Dispose all trees
        var entities = m_toRegister.ToEntityArray(Allocator.Temp);
        var colliders = m_toRegister.ToComponentDataArray<Collider>(Allocator.Temp);
        var quadElements = new NativeArray<QuadElement<Entity>>(colliders.Length, Allocator.Temp);
        
        for (int i = 0; i < colliders.Length; i++)
            quadElements[i] = colliders[i].ToQuadElement(entities[i]);
        survivorTree.ClearAndBulkInsert(quadElements);
        
        quadElements.Dispose();
        colliders.Dispose();
        entities.Dispose();
    
        var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
        new CollisionJob()
        {
            ecb = delayedEcb.AsParallelWriter(),
            projectiles = enemyProjectileTree
        }.Schedule();
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
        [ReadOnly] public NativeQuadTree<Entity> projectiles;

        public void Execute(Entity entity, in LocalTransform transform, in Collider collider, ref DynamicBuffer<ProjectileCharacterCollision> collisions)
        {
            var adjustedAABB2D = collider.Add(transform.Position.xy);
            var visitor = new CollisionVisitor(ref ecb, ref collisions);
            projectiles.RangeQuery();
            projectiles.Range(adjustedAABB2D, ref visitor);
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