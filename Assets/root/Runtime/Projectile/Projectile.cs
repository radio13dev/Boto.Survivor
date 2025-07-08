using Unity.Entities;
using Unity.NetCode;

[GhostComponent]
public struct Projectile : IComponentData
{
    public double DestroyTime;
}

public struct EnableRaycastCollisionDetect : IComponentData, IEnableableComponent{}