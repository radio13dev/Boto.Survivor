using Collisions;
using NativeTrees;
using Unity.Entities;
using Unity.Mathematics;
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
            AddComponent(entity, new DestroyAtTime());
            AddComponent(entity, new SurfaceMovement());
            AddComponent(entity, new LockToSurface());
            
            AddComponent<SurvivorProjectileTag>(entity);
        }
    }       
}