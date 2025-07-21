using BovineLabs.Saving;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// The 'auto attack' system for survivors. These are added as children to the survivor's LinkedEntityGroup
/// </summary>
[Save]
public struct LaserProjectileSpawner : IComponentData, IEnableableComponent
{
    public double Lifespan;
    public double TimeBetweenShots;
    
    public float3 LastProjectileDirection;
    public double LastProjectileTime;
}