using BovineLabs.Saving;
using Collisions;
using Unity.Entities;
using UnityEngine;

public class ProjectileAuthoring : MonoBehaviour
{
    public partial class Baker : Baker<ProjectileAuthoring>
    {
        public override void Bake(ProjectileAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new Projectile());
            
            AddComponent(entity, new ProjectileHit());
            SetComponentEnabled<ProjectileHit>(entity, false);
            AddBuffer<ProjectileHitEntity>(entity);
            AddBuffer<ProjectileIgnoreEntity>(entity);
            
            AddComponent(entity, new ProjectileLoopTrigger());
            AddComponent(entity, new DestroyAtTime());
            
            AddComponent<SurvivorProjectileTag>(entity);
        }
    }       
}