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
            AddComponent(entity, new Movement(1,1));
            
            AddComponent(entity, new Collider(new AABB2D(new float2(-1,-1), new float2(1,1))));
            SetComponentEnabled<Collider>(entity, false);
        }
    }       
}