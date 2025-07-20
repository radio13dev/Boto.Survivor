using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// The 'auto attack' system for survivors. These are added as children to the survivor's LinkedEntityGroup
/// </summary>
[Save]
public struct ProjectileSpawner : IComponentData, IEnableableComponent
{
    public double LastProjectileTime;
    public Team Team;
    
    public ProjectileSpawner(Team team)
    {
        LastProjectileTime = 0;
        this.Team = team;
    }
}


[UpdateInGroup(typeof(GameLogicSystemGroup))]
public partial struct SurvivorProjectileSpawnerSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameManager.Resources>();
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        new Job()
        {
            ecb = delayedEcb.AsParallelWriter(),
            CurrentTime = SystemAPI.Time.ElapsedTime,
            ProjectilePrefab = SystemAPI.GetSingleton<GameManager.Resources>().ProjectileTemplate
        }.ScheduleParallel();
    }
    
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public Entity ProjectilePrefab;
        [ReadOnly] public double CurrentTime;
    
        public void Execute([ChunkIndexInQuery] int key, Entity entity, in LocalTransform2D localTransform, ref ProjectileSpawner spawner)
        {
            if (CurrentTime - spawner.LastProjectileTime > 2)
            {
                spawner.LastProjectileTime = CurrentTime;
                
                for (int i = 0; i < 8; i++)
                {
                    var newProjectile = ecb.Instantiate(key, ProjectilePrefab);
                    ecb.SetComponent(key, newProjectile, new DestroyAtTime()
                    {
                        DestroyTime = CurrentTime + 20
                    });
                    var projectileT = localTransform;
                    projectileT.Rotation = i*math.PI2/8;
                    ecb.SetComponent(key, newProjectile, projectileT);
                    ecb.SetComponent(key, newProjectile, new Movement(0,0,10){ Velocity = projectileT.Forward });
                    
                    if (spawner.Team == Collisions.SurvivorProjectileTag.Team)
                        ecb.AddComponent<Collisions.SurvivorProjectileTag>(key, newProjectile);
                    else if (spawner.Team == Collisions.EnemyProjectileTag.Team)
                        ecb.AddComponent<Collisions.EnemyProjectileTag>(key, newProjectile);
                    else
                        Debug.Log($"Invalid collider index for spawner: {spawner.Team}");
                }
            }
        }
    }
}