using Unity.Entities;
using UnityEngine;

public class ProjectileChainAuthoring : MonoBehaviour
{
    public partial class Baker : Baker<ProjectileChainAuthoring>
    {
        public override void Bake(ProjectileChainAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new Projectile());
            AddComponent(entity, new ProjectileHit());
            AddBuffer<ProjectileHitEntity>(entity);
        }
    }
}