using System;
using Unity.Collections;
using Unity.Entities;

[Serializable]
public struct SpriteAnimData
{
    public uint Frames;
    public double TimeBetweenFrames;
}

public struct SpriteAnimFrame : IComponentData
{
    public float Frame;
}

public struct SpriteAnimFrameTime : IComponentData
{
    public double NextFrameTime;
}

public partial struct SpriteUpdateSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameManager.InstancedResources>();
    }

    public void OnUpdate(ref SystemState state)
    {
        new Job()
        {
            Time = SystemAPI.Time.ElapsedTime,
            InstanceData = SystemAPI.GetSingletonBuffer<GameManager.InstancedResources>()
        }.ScheduleParallel();
    }
    
    partial struct Job : IJobEntity
    {
        [ReadOnly] public double Time;
        [ReadOnly] public DynamicBuffer<GameManager.InstancedResources> InstanceData;
    
        public void Execute(ref SpriteAnimFrame frame, ref SpriteAnimFrameTime frameTime, in InstancedResourceRequest instance)
        {
            if (Time < frameTime.NextFrameTime) return;
            
            var animData = InstanceData[instance.ToSpawn].AnimData;
            frameTime.NextFrameTime = Time + animData.TimeBetweenFrames;
            frame.Frame++;
            if (frame.Frame > animData.Frames)
                frame.Frame = 0;
        }
    }
}