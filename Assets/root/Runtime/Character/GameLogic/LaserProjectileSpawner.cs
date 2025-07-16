using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// The 'auto attack' system for survivors. These are added as children to the survivor's LinkedEntityGroup
/// </summary>
public struct LaserProjectileSpawner : IComponentData, IEnableableComponent
{
    public float2 LastProjectileDirection;
    public double LastProjectileTime;
    public double TimeBetweenShots;
    public Team Team;
    
    public LaserProjectileSpawner(Team team)
    {
        LastProjectileDirection = new float2(1,0);
        LastProjectileTime = 0;
        TimeBetweenShots = 0.1d;
        this.Team = team;
    }
}