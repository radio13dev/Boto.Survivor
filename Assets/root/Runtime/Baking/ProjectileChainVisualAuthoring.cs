using Unity.Entities;
using UnityEngine;

public class ProjectileChainVisualAuthoring : MonoBehaviour
{
    public partial class Baker : Baker<ProjectileChainVisualAuthoring>
    {
        public override void Bake(ProjectileChainVisualAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new Chain.Visual());
        }
    }
}