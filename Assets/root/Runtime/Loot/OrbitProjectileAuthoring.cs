using BovineLabs.Saving;
using Collisions;
using NativeTrees;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Collider = Collisions.Collider;

[Save]
public struct OrbitProjectileData : IComponentData
{
    public const int TemplateIndex = SeekerProjectileData.TemplateIndex + 8;

    public double CreateTime;
    public byte SeekerCount;
    public NetworkId LockedTarget;
}

public class OrbitProjectileAuthoring : MonoBehaviour
{
    partial class Baker : Baker<OrbitProjectileAuthoring>
    {
        public override void Bake(OrbitProjectileAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new OrbitProjectileData());
            AddComponent(entity, new MovementSettings() { Speed = 1 });
            AddComponent(entity, new OwnedProjectile());
            AddComponent<Pierce>(entity, Pierce.Infinite);
            AddComponent<IgnoresTerrain>(entity);
        }
    }
}

[UpdateInGroup(typeof(ProjectileMovementSystemGroup))]
public partial struct OrbitProjectileSystem : ISystem
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
        [ReadOnly] public NativeOctree<(Entity e, NetworkId id, Collider c)> EnemyColliderTree;
        [ReadOnly] public NetworkIdMapping NetworkIdMapping;

        public void Execute(ref OrbitProjectileData orbit, in OwnedProjectile owned, in MovementSettings speed,
            in LocalTransform transform, ref DynamicBuffer<ProjectileIgnoreEntity> ignoredEntities,
            in Movement movement, ref Force force, ref RotationalInertia rotationalInertia)
        {
            // We can hit enemies every frame, so go nuts!
            ignoredEntities.Clear();
            
            {
                // Orbit player
                if (owned.PlayerId < 0 || owned.PlayerId >= Players.Length) return;
                var playerE = Players[owned.PlayerId].Value;
                if (!TransformLookup.TryGetComponent(playerE, out var playerT)) return;

                // ... determine orbit pos based on index
                var zero = playerT.Position;
                var fullRotTime = 360 / speed.Speed;
                var rotOffset = math.PI2 * (float)((Time % fullRotTime) / fullRotTime);
                var rot = quaternion.AxisAngle(playerT.Up(), rotOffset + math.PI2 * owned.Key.Index / orbit.SeekerCount);
                
                var perp = mathu.perpendicular(playerT.Up());
                zero += math.mul(rot, perp * 5);
                zero += playerT.Up() * 1;

                // ... move towards that position smoothly
                var predictedPos = transform.Position + movement.Velocity * dt * 2;
                force.Velocity += math.clamp(zero - predictedPos, -3, 3) * dt * speed.Speed;
            }
        }
    }
}