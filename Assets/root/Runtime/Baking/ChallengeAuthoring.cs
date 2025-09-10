using System;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class ChallengeAuthoring : MonoBehaviour
{
    public Challenge Challenge;
    
    partial class Baker : Baker<ChallengeAuthoring>
    {
        public override void Bake(ChallengeAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, authoring.Challenge);
            AddComponent(entity, new DestroyAtTime() { DestroyTime = double.MaxValue });
        }
    }
}

[Serializable]
[Save]
public struct Challenge : IComponentData
{
    public float timer;
}

public partial struct ChallengeSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<GameManager.Prefabs>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        new Job()
        {
            ecb = delayedEcb,
            dt = SystemAPI.Time.DeltaTime,
            Time = SystemAPI.Time.ElapsedTime,
            Prefabs = SystemAPI.GetSingletonBuffer<GameManager.Prefabs>()
        }.Schedule();
    }
    
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer ecb;
        [ReadOnly] public float dt;
        [ReadOnly] public double Time;
        [ReadOnly] public DynamicBuffer<GameManager.Prefabs> Prefabs;
    
        public void Execute(ref Challenge challenge, in LocalTransform transform)
        {
            var old = challenge.timer;
            challenge.timer += dt;
            
            var range = new float2(old % 10, challenge.timer % 10);
            if (range.Contains(1))
            {
                GameManager.Prefabs.SpawnTorusBlast(in Prefabs, ref ecb, transform, 0, 6, Time, 4);
                GameManager.Prefabs.SpawnTorusBlast(in Prefabs, ref ecb, transform, 10, 15, Time, 4);
                GameManager.Prefabs.SpawnTorusConeBlast(in Prefabs, ref ecb, transform, 6, 10, 0.3f, Time, 4);
            }
            else if (range.Contains(5.5f))
            {
                GameManager.Prefabs.SpawnTorusBlast(in Prefabs, ref ecb, transform, 6, 10, Time, 4);
                GameManager.Prefabs.SpawnTorusBlast(in Prefabs, ref ecb, transform, 15, 20, Time, 4);
            }
        }
    }
}