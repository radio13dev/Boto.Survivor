using System;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Collider = Collisions.Collider;

public class CirclableAuthoring : MonoBehaviour
{
    public Circlable Circlable;
    class Baker : Baker<CirclableAuthoring>
    {
        public override void Bake(CirclableAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, authoring.Circlable);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, Circlable.Radius);
    }
}

[Serializable]
[Save]
public struct Circlable : IComponentData
{
    public const float DrainDelay = 0.5f;
    public const float DrainRate = 0.5f;
    public const float ChargeRate = 1f;
    public const float PerpendicularAngleMax = math.PI / 6f;

    public float Radius;
    [HideInInspector] public float Charge;
    public float MaxCharge;
    [HideInInspector] public double LastChargeTime;
}

public partial struct CirclableSystem : ISystem
{
    EntityQuery m_CirclersQuery;

    public void OnCreate(ref SystemState state)
    {
        m_CirclersQuery = SystemAPI.QueryBuilder().WithAll<SurvivorTag, Movement, LocalTransform>().Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        var circlers = m_CirclersQuery.ToEntityArray(Allocator.TempJob);
        state.Dependency = new Job()
        {
            dt = SystemAPI.Time.DeltaTime,
            Time = SystemAPI.Time.ElapsedTime,
            Circlers = circlers,
            MovementLookup = SystemAPI.GetComponentLookup<Movement>(true),
            TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
        }.Schedule(state.Dependency);
        circlers.Dispose(state.Dependency);
    }
    
    partial struct Job : IJobEntity
    {
        [ReadOnly] public float dt;
        [ReadOnly] public double Time;
        [ReadOnly] public NativeArray<Entity> Circlers;
        [ReadOnly] public ComponentLookup<Movement> MovementLookup;
        [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    
        public void Execute(in LocalTransform transform, ref Circlable c)
        {
            bool didCharge = false;
            for (int i = 0; i < Circlers.Length; i++)
            {
                var t = TransformLookup[Circlers[i]];
                var d = math.distancesq(t.Position, transform.Position);
                if (d < c.Radius * c.Radius)
                {
                    // If not moving, skip
                    var m = MovementLookup[Circlers[i]];
                    if (math.lengthsq(m.Velocity) < math.EPSILON) continue;
                    
                    // Only charge if the circlir is moving perpendicular to transform.
                    var toCenter = math.normalizesafe(transform.Position - t.Position);
                    var moveDir = math.normalizesafe(m.Velocity);
                    var dot = math.dot(toCenter, moveDir);
                    var maxAngleFromPerp = math.sin(Circlable.PerpendicularAngleMax);
                    
                    if (math.abs(dot) < maxAngleFromPerp)
                    {
                        float chargeToAdd = dt * Circlable.ChargeRate * (1.0f - (math.abs(dot) / maxAngleFromPerp));
                        c.Charge = math.min(c.MaxCharge, c.Charge + chargeToAdd);
                        c.LastChargeTime = Time;
                        didCharge = true;
                        break;
                    }
                }
            }
            
            if (!didCharge && Time - c.LastChargeTime > Circlable.DrainDelay)
            {
                c.Charge = math.max(0, c.Charge - dt*Circlable.DrainRate);
            }
        }
    }
}