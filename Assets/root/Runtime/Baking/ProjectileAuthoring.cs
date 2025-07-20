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
            AddComponent<LocalTransform2D>(entity);
            AddComponent(entity, new ProjectileTag());
            AddComponent(entity, new DestroyAtTime());
            AddComponent(entity, new Movement(0,0,10));
        }
    }       
}