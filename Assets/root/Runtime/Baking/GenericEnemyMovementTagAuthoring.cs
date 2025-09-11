using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct GenericEnemyMovementTag : IComponentData { }

public class GenericEnemyMovementTagAuthoring : MonoBehaviour
{
    public class GenericEnemyMovementTagBaker : Baker<GenericEnemyMovementTagAuthoring>
    {
        public override void Bake(GenericEnemyMovementTagAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<GenericEnemyMovementTag>(entity);
        }
    }
}

[RequireMatchingQueriesForUpdate]
[BurstCompile]
[UpdateInGroup(typeof(MovementSystemGroup))]
public partial struct GenericEnemyMovementSystem : ISystem
{
    EntityQuery m_TargetQuery;

    public void OnCreate(ref SystemState state)
    {
        m_TargetQuery = SystemAPI.QueryBuilder().WithAll<PlayerControlled, LocalTransform>().Build();
        state.RequireForUpdate(m_TargetQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // TODO: Order this list.
        var targets = m_TargetQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        
        var a = new GenericEnemyMovementJob()
        {
            Targets = targets,
        }.Schedule(state.Dependency);
        var b = new WheelEnemyMovementJob()
        {
            dt = SystemAPI.Time.DeltaTime,
            Targets = targets,
        }.Schedule(a);
        
        state.Dependency = b;
        targets.Dispose(state.Dependency);
    }
    
    [BurstCompile]
    [WithAll(typeof(GenericEnemyMovementTag))]
    partial struct GenericEnemyMovementJob : IJobEntity
    {   
        [ReadOnly] public NativeArray<LocalTransform> Targets;
        public void Execute(in LocalTransform localTransform, in Movement movement, ref StepInput input)
        {
            float3 dir = float3.zero;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Targets.Length; i++)
            {
                var dif = math.abs(localTransform.Position - Targets[i].Position);
                var dist = dif.x + dif.y + dif.z;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    dir = Targets[i].Position - localTransform.Position;
                }
            }
            input = new StepInput(){Direction = dir };
        }
    }
    
    [BurstCompile]
    [WithPresent(typeof(MovementInputLockout))]
    partial struct WheelEnemyMovementJob : IJobEntity
    {   
        public const float LOCK_ON_RANGE = 20f;
    
        [ReadOnly] public float dt;
        [ReadOnly] public NativeArray<LocalTransform> Targets;
        public void Execute(in LocalTransform localTransform, in Movement movement, EnabledRefRW<MovementInputLockout> movementLockout, ref WheelEnemyMovement wheel, ref StepInput input, ref Force force)
        {
            switch (wheel.state)
            {
                // Do generic movement until we're in range of someone
                case 0:
                default:
                {
                    float3 dir = float3.zero;
                    float bestDist = float.MaxValue;
                    int bestTarget = -1;
                    for (int i = 0; i < Targets.Length; i++)
                    {
                        var dif = math.abs(localTransform.Position - Targets[i].Position);
                        var dist = dif.x + dif.y + dif.z;
                        if (dist < bestDist)
                        {
                            bestTarget = i;
                            bestDist = dist;
                            dir = Targets[i].Position - localTransform.Position;
                        }
                    }
                    input = new StepInput(){Direction = dir };
                    
                    if (bestDist < LOCK_ON_RANGE)
                    {
                        wheel.target = (byte)bestTarget;
                        wheel.state = 1;
                        wheel.timer = 0;
                        movementLockout.ValueRW = true;
                    }
                    
                    break;
                }
                
                // Once in range, start charge anim
                case 1:
                {
                    if (wheel.target >= Targets.Length)
                    {
                        // Reset
                        wheel.state = 0;
                        wheel.timer = 0;
                        movementLockout.ValueRW = false;
                        break;
                    }
                
                    // Maintain lock on target for most of this time
                    if (wheel.timer <= 1.5f)
                    {
                        var dir = Targets[wheel.target].Position - localTransform.Position;
                        input = new StepInput(){Direction = dir };
                    }
                
                    // Idle in charge anim for time
                    if ((wheel.timer += dt) >= 2)
                    {
                        wheel.state = 2;
                        wheel.timer = 0;
                    }
                    break;
                }
                
                // Start charge attack anim
                case 2:
                {
                    // Charge in our last input direction
                    force.Velocity += input.Direction*dt*20;
                
                    // Idle in charge attack anim
                    if ((wheel.timer += dt) >= 1)
                    {
                        wheel.state = 3;
                        wheel.timer = 0;
                    }
                    break;
                }
            
            
                // End charge attack anim
                case 3:
                {
                    // Idle in charge attack anim end for time
                    if ((wheel.timer += dt) >= 2)
                    {
                        // Reset
                        wheel.state = 0;
                        wheel.timer = 0;
                        movementLockout.ValueRW = false;
                    }
                    break;
                }
            }
        }
    }
}