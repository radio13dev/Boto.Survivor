using BovineLabs.Saving;
using Collisions;
using Unity.Entities;

public struct Projectile : IComponentData
{
    public float Damage;
}

[Save]
public struct ProjectileHit : IComponentData, IEnableableComponent
{
    public Entity HitEntity;
}

[UpdateBefore(typeof(CollisionSystemGroup))]
[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
public partial class ProjectileSystemGroup : ComponentSystemGroup
{
}