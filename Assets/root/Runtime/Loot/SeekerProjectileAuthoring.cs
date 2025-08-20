using Collisions;
using NativeTrees;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct SeekerProjectileData : IComponentData
{
    public double CreateTime;
    public byte SeekerCount;
    public NetworkId LockedTarget;
}

public class SeekerProjectileAuthoring : MonoBehaviour
{
    partial class Baker : Baker<SeekerProjectileAuthoring>
    {
        public override void Bake(SeekerProjectileAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new SeekerProjectileData());
            AddComponent(entity, new MovementSettings(){ Speed = 1 });
            AddComponent(entity, new OwnedProjectile());
        }
    }
}

public partial struct SeekerProjectileSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkIdMapping>();
        state.RequireForUpdate<PlayerControlledLink>();
    }

    public void OnUpdate(ref SystemState state)
    {
        new Job()
        {
            dt = SystemAPI.Time.DeltaTime,
            Time = SystemAPI.Time.ElapsedTime,
            TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
            Players = SystemAPI.GetSingletonBuffer<PlayerControlledLink>(true),
            EnemyColliderTree = state.WorldUnmanaged.GetUnsafeSystemRef<EnemyColliderTreeSystem>(state.WorldUnmanaged.GetExistingUnmanagedSystem<EnemyColliderTreeSystem>()).Tree,
            NetworkIdMapping = SystemAPI.GetSingleton<NetworkIdMapping>()
        }.Schedule();
    }
    
    [WithPresent(typeof(OwnedProjectile))]
    partial struct Job : IJobEntity
    {
        [ReadOnly] public float dt;
        [ReadOnly] public double Time;
        [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
        [ReadOnly] public DynamicBuffer<PlayerControlledLink> Players;
        [ReadOnly] public NativeOctree<(Entity, NetworkId)> EnemyColliderTree;
        [ReadOnly] public NetworkIdMapping NetworkIdMapping;
    
        public void Execute(ref SeekerProjectileData seeker, in OwnedProjectile owned, in MovementSettings speed, 
            in LocalTransform transform, in DynamicBuffer<ProjectileIgnoreEntity> ignoredEntities,
            in Movement movement, ref Force force, ref RotationalInertia rotationalInertia)
        {
            var aliveTime = Time - seeker.CreateTime;
            if (aliveTime > 2 && !TransformLookup.HasComponent(NetworkIdMapping[seeker.LockedTarget]))
            {
                var visitor = new EnemyColliderTree.NearestVisitor();
                var distance = new EnemyColliderTree.DistanceProvider();
                EnemyColliderTree.Nearest(transform.Position, 30, ref visitor, distance);
                if (visitor.Hits > 0)
                {
                    // Lock on to that target and fly towards them
                    seeker.LockedTarget = visitor.NearestObj.Item2;
                }
            }
            if (TransformLookup.TryGetComponent(NetworkIdMapping[seeker.LockedTarget], out var targetT))
            {
                // ... move towards that position smoothly
                var predictedPos = transform.Position;// + movement.Velocity*dt*10;
                force.Velocity += (targetT.Position - predictedPos)*dt*speed.Speed;
            }
            else
            {
                // If we can't find an enemy to target, fallback to orbiting the player.
            
                // Orbit player
                if (owned.PlayerId < 0 || owned.PlayerId >= Players.Length) return;
                var playerE = Players[owned.PlayerId].Value;
                if (!TransformLookup.TryGetComponent(playerE, out var playerT)) return;
                
                // ... determine orbit pos based on index
                var zero = playerT.Position;// + playerT.Up()*5;
                var rot = quaternion.AxisAngle(playerT.Up(), math.PI2 * owned.Key.Index/seeker.SeekerCount);
                zero += math.mul(rot, playerT.Forward()*2);
                zero += playerT.Up()*5;
                
                // ... move towards that position smoothly
                var predictedPos = transform.Position + movement.Velocity*dt*2;
                force.Velocity += (zero - predictedPos)*dt*speed.Speed;
            }
        }
    }
}