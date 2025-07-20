using BovineLabs.Saving;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// The 'auto attack' system for survivors. These are added as children to the survivor's LinkedEntityGroup
/// </summary>
[Save]
public struct LaserProjectileSpawner : IComponentData, IEnableableComponent
{
    public Team Team;
    
    public double Lifespan;
    public double TimeBetweenShots;
    
    public float2 LastProjectileDirection;
    public double LastProjectileTime;
    
    public LaserProjectileSpawner(Team team)
    {
        this.Team = team;
        
        Lifespan = 0;
        TimeBetweenShots = 0.0d;
        
        LastProjectileDirection = new float2(1,0);
        LastProjectileTime = 0;
    }
}