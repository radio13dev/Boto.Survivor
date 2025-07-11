using Unity.Entities;

/// <summary>
/// The 'auto attack' system for survivors. These are added as children to the survivor's LinkedEntityGroup
/// </summary>
public struct LaserProjectileSpawner : IComponentData, IEnableableComponent
{
    public double LastProjectileTime;
    public Team Team;
    
    public LaserProjectileSpawner(Team team)
    {
        LastProjectileTime = 0;
        this.Team = team;
    }
}