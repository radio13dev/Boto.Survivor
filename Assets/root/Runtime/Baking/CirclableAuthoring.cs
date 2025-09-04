using System;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

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
                    // Only charge if the circlir is moving perpendicular to transform.
                    var m = MovementLookup[Circlers[i]];
                    var toCenter = math.normalize(transform.Position - t.Position);
                    var moveDir = math.normalize(m.Velocity);
                    var dot = math.dot(toCenter, moveDir);
                    if (dot < 0.5f)
                    {
                        c.Charge = math.min(c.MaxCharge, c.Charge + dt*(1-dot));
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