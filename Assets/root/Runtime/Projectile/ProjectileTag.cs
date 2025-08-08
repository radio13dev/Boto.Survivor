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
}

[Save]
public struct ProjectileHitEntity : IBufferElementData
{
    public Entity Value;

    public ProjectileHitEntity(Entity value)
    {
        Value = value;
    }
}

[Save]
public struct ProjectileIgnoreEntity : IBufferElementData
{
    public Entity Value;

    public ProjectileIgnoreEntity(Entity value)
    {
        Value = value;
    }
}

[UpdateBefore(typeof(CollisionSystemGroup))]
[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
public partial class ProjectileSystemGroup : ComponentSystemGroup
{
}