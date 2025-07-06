using Unity.Entities;

public struct Projectile : IComponentData
{
    public double DestroyTime;
}

public struct EnableRaycastCollisionDetect : IComponentData, IEnableableComponent{}