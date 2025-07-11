using Unity.Entities;

public struct ProjectileTag : IComponentData { }

public enum Team
{
    Survivor,
    Enemy
}

public struct DestroyAtTime : IComponentData
{
    public double DestroyTime;
}