using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// The 'auto attack' system for survivors. These are added as children to the survivor's LinkedEntityGroup
/// </summary>
[GhostComponent]
public struct ProjectileSpawner : IComponentData
{
    public double LastProjectileTime;
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
    
        public void Execute([ChunkIndexInQuery] int key, Entity entity, in LocalTransform localTransform, ref ProjectileSpawner spawner)
        {
            if (CurrentTime - spawner.LastProjectileTime > 2)
            {
                spawner.LastProjectileTime = CurrentTime;
                Debug.Log($"Spawning projectiles...");
                
                for (int i = 0; i < 8; i++)
                {
                    var newProjectile = ecb.Instantiate(key, ProjectilePrefab);
                    ecb.SetComponent(key, newProjectile, new Projectile()
                    {
                        DestroyTime = CurrentTime + 5
                    });
                    var projectileT = localTransform;
                    projectileT = projectileT.RotateZ(i*math.PI2/8);
                    ecb.SetComponent(key, newProjectile, projectileT);
                    ecb.SetComponent(key, newProjectile, new Movement(0,0,10){ Velocity = projectileT.Up().xy });
                }
            }
        }
    }
}