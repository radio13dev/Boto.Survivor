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
public struct MeleeProjectileData : IComponentData
{
    public const int TemplateIndex = OrbitProjectileData.TemplateIndex + 8;
    public const float SheathAngle = 90f;
    public const float SheathRadius = 2f;
    public const float SwingRadius = 3f;
    public const float SwingDelay = 0.5f;
    public const float RotRateModifier = 15f;

    public float SwingCooldown;
    public bool SwingToggle;
    public double CreateTime;
    public byte ProjectileCount;
    public float ProjectileSize;
    public NetworkId LockedTarget;
}

public class MeleeProjectileAuthoring : MonoBehaviour
{
    partial class Baker : Baker<MeleeProjectileAuthoring>
    {
        public override void Bake(MeleeProjectileAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new MeleeProjectileData());
            AddComponent(entity, new MovementSettings() { Speed = 2 });
            AddComponent(entity, new OwnedProjectile());
            AddComponent<Pierce>(entity, Pierce.Infinite);
        }
    }
}

[UpdateInGroup(typeof(ProjectileMovementSystemGroup))]
public partial struct MeleeProjectileSystem : ISystem
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
    [WithPresent(typeof(Collider))]
    partial struct Job : IJobEntity
    {
        [ReadOnly] public float dt;
        [ReadOnly] public double Time;
        [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
        [ReadOnly] public DynamicBuffer<PlayerControlledLink> Players;
        [ReadOnly] public NativeOctree<(Entity e, NetworkId id, Collider c)> EnemyColliderTree;
        [ReadOnly] public NetworkIdMapping NetworkIdMapping;
        
        public void Execute(ref MeleeProjectileData projectile, in OwnedProjectile owned, in MovementSettings speed,
            in LocalTransform transform, ref DynamicBuffer<ProjectileIgnoreEntity> ignoredEntities,
            in Movement movement, ref Force force, ref RotationalInertia rotationalInertia)
        {
            // Check for player
            if (owned.PlayerId < 0 || owned.PlayerId >= Players.Length) return;
            var playerE = Players[owned.PlayerId].Value;
            if (!TransformLookup.TryGetComponent(playerE, out var playerT)) return;
            
            // Spread behind player
            var sheathAngle = MeleeProjectileData.SheathAngle * math.PI / 180;
            var i = owned.Key.Index;
            var direction = (i % 2 == 0 ? -1 : 1);
            var angleBetween = projectile.ProjectileCount <= 1 ? 0f : sheathAngle / (projectile.ProjectileCount - 1);
            var angleOffset = projectile.ProjectileCount % 2 == 1 ? math.ceil(i / 2f) : math.floor(i / 2f) + 0.5f;
            angleOffset *= angleBetween * direction;
            var radius = MeleeProjectileData.SheathRadius;
            var targetRot = quaternion.AxisAngle(playerT.Up(), angleOffset);
            var targetPos = playerT.Position + math.mul(targetRot, -playerT.Forward() * radius);
            targetPos += playerT.Up() * 1;
            
            // ... move towards that position smoothly
            var predictedPos = transform.Position + movement.Velocity * dt * 4;
            force.Velocity += math.clamp(targetPos - predictedPos, -5, 5) * dt * speed.Speed;
            
            // Rotate to face away from player
            var targetForward = math.normalize(transform.Position - playerT.Position);
            var currentForward = math.mul(transform.Rotation, math.forward());

            // ... project both directions onto plane
            var targetFwdOnPlane = math.normalize(targetForward - math.project(targetForward, playerT.Up()));
            var currentFwdOnPlane = math.normalize(currentForward - math.project(currentForward, playerT.Up()));

            // ... get angle between projections
            var rotAngleDifference = math.atan2(
                math.dot(playerT.Up(), math.cross(currentFwdOnPlane, targetFwdOnPlane)), 
                math.dot(currentFwdOnPlane, targetFwdOnPlane));
            
            // ... set rotation rate
            var rotRate = rotAngleDifference * MeleeProjectileData.RotRateModifier;
            rotationalInertia.Set(playerT.Up(), rotRate);
        }
    }
}