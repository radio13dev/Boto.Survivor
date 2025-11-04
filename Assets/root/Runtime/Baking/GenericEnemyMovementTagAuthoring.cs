using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        state.RequireForUpdate<SharedRandom>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        m_TargetQuery = SystemAPI.QueryBuilder().WithAll<PlayerControlled, LocalTransform, EnemySpawner>().Build();
        state.RequireForUpdate(m_TargetQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // TODO: Order this list.
        var targets = m_TargetQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var sharedRandom = SystemAPI.GetSingleton<SharedRandom>();
        
        var a = new GenericEnemyMovementJob()
        {
            Random = sharedRandom,
            Targets = targets,
        }.Schedule(state.Dependency);
        var b = new WheelEnemyMovementJob()
        {
            Random = sharedRandom,
            dt = SystemAPI.Time.DeltaTime,
            Targets = targets,
        }.Schedule(a);
        var c = new EnemyTrapMovementJob()
        {
            Random = sharedRandom,
            ecb = delayedEcb,
            Time = SystemAPI.Time.ElapsedTime,
            dt = SystemAPI.Time.DeltaTime,
            Targets = targets,
            Prefabs = SystemAPI.GetSingletonBuffer<GameManager.Prefabs>(true)
        }.Schedule(b);
        
        state.Dependency = c;
        targets.Dispose(state.Dependency);
    }
    
    const float MAX_FOLLOW_DIST_SQR = 100f*100f;
    private static void GetClosest(in NativeArray<LocalTransform> targets, in float3 p, out float bestDistSqr, out int bestTarget)
    {
        bestDistSqr = MAX_FOLLOW_DIST_SQR;
        bestTarget = -1;
        for (int i = 0; i < targets.Length; i++)
        {
            var distSqr = math.distancesq(p, targets[i].Position);
            if (distSqr < bestDistSqr)
            {
                bestTarget = i;
                bestDistSqr = distSqr;
            }
        }
    }

    private static void DoIdleMovement(SharedRandom random, ref StepInput input, in LocalTransform localTransform)
    {
        const int WANDER_SEED_MOD = 2000;
        const int WANDER_CHANCE = 4;
        const int IDLE_CHANCE = 30;
    
        // Idle wander
        var r = random.Random;
        var i = (math.abs(r.NextInt()) + (int)math.abs(math.floor(
            localTransform.Position.x*7 + 
            localTransform.Position.y*13 + 
            localTransform.Position.z*5)
        ))%WANDER_SEED_MOD;
        if (i <= WANDER_CHANCE)
        {
            input = new StepInput(){Direction = r.NextFloat3Direction() };
        }
        else if (i <= IDLE_CHANCE)
        {
            input = default;
        }
    }
    
    [BurstCompile]
    [WithAll(typeof(GenericEnemyMovementTag))]
    partial struct GenericEnemyMovementJob : IJobEntity
    {   
        [ReadOnly] public SharedRandom Random;
        [ReadOnly] public NativeArray<LocalTransform> Targets;
        public void Execute(in LocalTransform localTransform, in Movement movement, ref StepInput input)
        {
        
            GetClosest(in Targets, in localTransform.Position, out float bestDistSqr, out int bestTarget);
            if (bestTarget == -1)
            {
                DoIdleMovement(Random, ref input, in localTransform);
            }
            else
            {
                input = new StepInput(){Direction = Targets[bestTarget].Position - localTransform.Position };
            }
        }
    }
    
    [BurstCompile]
    [WithPresent(typeof(MovementInputLockout))]
    partial struct WheelEnemyMovementJob : IJobEntity
    {   
        public const float LOCK_ON_RANGE_SQR = 400f;
    
        [ReadOnly] public SharedRandom Random;
        [ReadOnly] public float dt;
        [ReadOnly] public NativeArray<LocalTransform> Targets;
        public void Execute(in LocalTransform localTransform, in Movement movement, EnabledRefRW<MovementInputLockout> movementLockout, ref WheelEnemyMovement wheel, ref StepInput input, ref Force force)
        {
            switch (wheel.state)
            {
                // Do generic movement until we're in range of someone
                case WheelEnemyMovement.States.Idle:
                default:
                {
                    GetClosest(in Targets, in localTransform.Position, out float bestDistSqr, out int bestTarget);
                    if (bestTarget == -1)
                    {
                        DoIdleMovement(Random, ref input, in localTransform);
                    }
                    else
                    {
                        input = new StepInput(){Direction = Targets[bestTarget].Position - localTransform.Position };
                    
                        if (bestDistSqr < LOCK_ON_RANGE_SQR)
                        {
                            wheel.target = (byte)bestTarget;
                            wheel.state = WheelEnemyMovement.States.ChargeStart;
                            wheel.timer = 0;
                            movementLockout.ValueRW = true;
                        }
                    }
                    
                    break;
                }
                
                // Once in range, start charge anim
                case WheelEnemyMovement.States.ChargeStart:
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
                    var dir = Targets[wheel.target].Position - localTransform.Position;
                    input = new StepInput(){Direction = dir };
                
                    // Idle in charge anim for time
                    if ((wheel.timer += dt) >= 1.5f)
                    {
                        wheel.state = WheelEnemyMovement.States.ChargeAimingComplete;
                        wheel.timer = 0;
                    }
                    break;
                }
                
                // Idle for a short time after aiming
                case WheelEnemyMovement.States.ChargeAimingComplete:
                {
                    if ((wheel.timer += dt) >= 0.5f)
                    {
                        wheel.state = WheelEnemyMovement.States.ChargeAttack;
                        wheel.timer = 0;
                    }
                    break;
                }
                
                // Start charge attack anim
                case WheelEnemyMovement.States.ChargeAttack:
                {
                    // Charge in our last input direction
                    force.Velocity += math.normalizesafe(input.Direction)*dt*200;
                
                    // Idle in charge attack anim
                    if ((wheel.timer += dt) >= 1)
                    {
                        wheel.state = WheelEnemyMovement.States.ChargeEnd;
                        wheel.timer = 0;
                    }
                    break;
                }
            
            
                // End charge attack anim
                case WheelEnemyMovement.States.ChargeEnd:
                {
                    // Idle in charge attack anim end for time
                    if ((wheel.timer += dt) >= 3)
                    {
                        // Reset
                        wheel.state = WheelEnemyMovement.States.Idle;
                        wheel.timer = 0;
                        movementLockout.ValueRW = false;
                    }
                    break;
                }
            }
        }
    }
    
    [BurstCompile]
    [WithPresent(typeof(MovementInputLockout))]
    partial struct EnemyTrapMovementJob : IJobEntity
    {   
        public const float LOCK_ON_RANGE_SQR = 400f;
    
        public EntityCommandBuffer ecb;
        [ReadOnly] public SharedRandom Random;
        [ReadOnly] public double Time;
        [ReadOnly] public float dt;
        [ReadOnly] public NativeArray<LocalTransform> Targets;
        [ReadOnly] public DynamicBuffer<GameManager.Prefabs> Prefabs;
        
        public void Execute(in NetworkId myNetworkId, in LocalTransform localTransform, in Movement movement, EnabledRefRW<MovementInputLockout> movementLockout, ref EnemyTrapMovement trap, ref StepInput input, ref Force force)
        {
            switch (trap.state)
            {
                // Do generic movement until we're in range of someone
                case EnemyTrapMovement.States.Idle:
                default:
                {
                    GetClosest(in Targets, in localTransform.Position, out float bestDistSqr, out int bestTarget);
                    if (bestTarget == -1)
                    {
                        DoIdleMovement(Random, ref input, in localTransform);
                    }
                    else
                    {
                        input = new StepInput(){Direction = Targets[bestTarget].Position - localTransform.Position };
                    
                        if (bestDistSqr < LOCK_ON_RANGE_SQR)
                        {
                            trap.target = (byte)bestTarget;
                            trap.state = EnemyTrapMovement.States.Charge;
                            trap.timer = 0;
                            movementLockout.ValueRW = true;
                        }
                    }
                    
                    break;
                }
                
                // Once in range, start charge anim
                case EnemyTrapMovement.States.Charge:
                {
                    if (trap.target >= Targets.Length)
                    {
                        // Reset
                        trap.state = 0;
                        trap.timer = 0;
                        movementLockout.ValueRW = false;
                        break;
                    }
                
                    // Maintain lock on target for most of this time
                    var dir = Targets[trap.target].Position - localTransform.Position;
                    input = new StepInput(){Direction = dir };
                
                    // Idle in charge anim for time
                    var range = math.float2(trap.timer, trap.timer += dt);
                    if (range.Contains(1.15f))
                    {
                        // Create projectile, lock its movement, its position will follow a custom anim curve
                        GameManager.Prefabs.SpawnTrapProjectile(in Prefabs, ref ecb, in myNetworkId, in localTransform, in Time);
                    }
                    
                    if (trap.timer >= 3f)
                    {
                        trap.state = 0;
                        trap.timer = 0;
                        movementLockout.ValueRW = false;
                    }
                    break;
                }
            }
        }
    }
}

public struct EnemyTrapProjectileAnimation : IComponentData
{
    public bool Released;
    public NetworkId ParentId;
}

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(ProjectileMovementSystemGroup))]
public partial struct EnemyTrapProjectileSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkIdMapping>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var mapping = SystemAPI.GetSingleton<NetworkIdMapping>();
        foreach (var (transformRW, projectileRW, e) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<EnemyTrapProjectileAnimation>>().WithEntityAccess())
        {
            if (projectileRW.ValueRO.Released)
            {
                continue;
            }
        
            var parentE = mapping[projectileRW.ValueRO.ParentId];
            if (!SystemAPI.HasComponent<EnemyTrapMovement>(parentE))
            {
                SystemAPI.SetComponentEnabled<DestroyFlag>(e, true);
                continue;
            }
            
            var trap = SystemAPI.GetComponent<EnemyTrapMovement>(parentE);
            if (trap.timer <= 1.5f)
            {
                var trapT = SystemAPI.GetComponent<LocalTransform>(parentE);
                var t = math.clamp((trap.timer - 1.15f)/(1.3f - 1.15f), 0, 1);
                var p = trapT.TransformPoint(math.lerp(math.float3(0,0.5f,0), math.float3(0, 1.88f, -1.37f), t));
                var r = trapT.Rotation;
                var s = math.lerp(0, trapT.Scale, t);
                transformRW.ValueRW = LocalTransform.FromPositionRotationScale(p, r, s); 
            }
            else
            {
                SystemAPI.SetComponent(e, new Force()
                {
                    Velocity = transformRW.ValueRO.TransformDirection(math.float3(0,5,30))
                });
                SystemAPI.SetComponent(e, new RotationalInertia(transformRW.ValueRO.Right(), 30));
                projectileRW.ValueRW.Released = true;
                SystemAPI.SetComponentEnabled<MovementDisabled>(e, false);
            }
        }
    }
}