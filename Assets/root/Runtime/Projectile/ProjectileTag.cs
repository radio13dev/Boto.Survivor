using BovineLabs.Saving;
using Collisions;
using Unity.Collections;
using Unity.Entities;

public struct ProjectileTag : IComponentData
{
    
}

[Save]
public struct ProjectileHit : IComponentData, IEnableableComponent
{
    public Entity HitEntity;
}

[UpdateBefore(typeof(CollisionSystemGroup))]
public partial class ProjectileSystemGroup : ComponentSystemGroup
{
}