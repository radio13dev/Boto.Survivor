using System;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class CirclingAuthoring : MonoBehaviour
{
    public Circling Circling;
    class Baker : Baker<CirclingAuthoring>
    {
        public override void Bake(CirclingAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, authoring.Circling);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, Circling.Radius);
    }
}

[Serializable]
[Save]
public struct Circling : IComponentData
{
    public const float DrainDelay = 0.5f;
    public const float DrainRate = 0.5f;
    public const float ChargeRate = 1f;
    public const float PerpendicularAngleMax = math.PI / 6f;

    public float Radius;
    [HideInInspector] public float Charge;
    public float MaxCharge;
    [HideInInspector] public double LastChargeTime;
    
    public float Percentage => Charge / MaxCharge;
}
public partial struct CirclingSystem : ISystem
{
    EntityQuery m_CirclableQuery;

    public void OnCreate(ref SystemState state)
    {
        m_CirclableQuery = SystemAPI.QueryBuilder().WithAll<Circlable, LocalTransform>().Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        var circles = m_CirclableQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var circleColliders = m_CirclableQuery.ToComponentDataArray<Circlable>(Allocator.TempJob);
        state.Dependency = new Job()
        {
            dt = SystemAPI.Time.DeltaTime,
            Time = SystemAPI.Time.ElapsedTime,
            Circles = circles,
            CircleColliders = circleColliders
        }.Schedule(state.Dependency);
        circles.Dispose(state.Dependency);
        circleColliders.Dispose(state.Dependency);
    }
    
    partial struct Job : IJobEntity
    {
        [ReadOnly] public float dt;
        [ReadOnly] public double Time;
        [ReadOnly] public NativeArray<LocalTransform> Circles;
        [ReadOnly] public NativeArray<Circlable> CircleColliders;
    
        public void Execute(in LocalTransform transform, in Movement movement, ref Circling circling)
        {
            bool didCharge = false;
            for (int i = 0; i < Circles.Length; i++)
            {
                var circleT = Circles[i];
                var d = math.distancesq(circleT.Position, transform.Position);
                var circleC = CircleColliders[i];
                if (d < circleC.Radius * circleC.Radius)
                {
                    // If not moving, skip
                    if (math.lengthsq(movement.Velocity) < math.EPSILON) continue;
                    
                    // Only charge if the circlir is moving perpendicular to transform.
                    var toCenter = math.normalizesafe(transform.Position - circleT.Position);
                    var moveDir = math.normalizesafe(movement.Velocity);
                    var dot = math.dot(toCenter, moveDir);
                    var maxAngleFromPerp = math.sin(Circling.PerpendicularAngleMax);
                    
                    if (math.abs(dot) < maxAngleFromPerp)
                    {
                        float chargeToAdd = dt * Circling.ChargeRate * (1.0f - (math.abs(dot) / maxAngleFromPerp));
                        circling.Charge = math.min(circling.MaxCharge, circling.Charge + chargeToAdd);
                        circling.LastChargeTime = Time;
                        didCharge = true;
                        break;
                    }
                }
            }
            
            if (!didCharge && Time - circling.LastChargeTime > Circling.DrainDelay)
            {
                circling.Charge = math.max(0, circling.Charge - dt*Circling.DrainRate);
            }
        }
    }
}