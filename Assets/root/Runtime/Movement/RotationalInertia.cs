using BovineLabs.Saving;
using Collisions;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[Save]
public struct RotationalInertia : IComponentData
{
    public float3 Normal;
    public float Rate;
    public quaternion Rotation;
    
    public void Set(float3 normal, float rate)
    {
        Normal = normal;
        Rate = rate;
        Rotation = quaternion.AxisAngle(normal, rate);
    }
}

[BurstCompile]
public partial struct RotationalIntertiaSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        new UpdateJob().Schedule();
        new ApplyJob().Schedule();
    }
    
    [BurstCompile]
    partial struct UpdateJob : IJobEntity
    {
        [ReadOnly] public float dt;
        public void Execute(ref RotationalInertia inertia, in PhysicsResponse physicsResponse)
        {
            if (inertia.Rate != 0)
            {
                var finalRate = inertia.Rate;
                if (physicsResponse.RotationalDrag != 0) finalRate -= finalRate * physicsResponse.RotationalDrag * dt;
                if (physicsResponse.RotationalDragLinear != 0) finalRate = mathu.MoveTowards(finalRate, 0, physicsResponse.FrictionLinear*dt);
                inertia.Set(inertia.Normal, finalRate);
            }
        }
    }
    [BurstCompile]
    partial struct ApplyJob : IJobEntity
    {
        public void Execute(in RotationalInertia inertia, ref LocalTransform transform)
        {
            if (inertia.Rate != 0)
                transform.Rotation = math.normalizesafe(math.mul(inertia.Rotation, transform.Rotation));
        }
    }
}