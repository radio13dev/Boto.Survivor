using Unity.Entities;
using Unity.NetCode;

public struct ProjectileTag : IComponentData { }

public enum Team
{
    Survivor,
    Enemy
}

[GhostComponent]
public struct DestroyAtTime : IComponentData
{
    public double DestroyTime;
}