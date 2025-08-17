using Unity.Entities;

/// <summary>
/// Projectile collision is predicted, only after all movement is done
/// </summary>
public partial class SurvivorSimulationSystemGroup : ComponentSystemGroup
{
    protected override void OnCreate()
    {
        base.OnCreate();
        Enabled = false; // Disable by default, enable when needed
    }
}

public partial class WorldInitSystemGroup : ComponentSystemGroup
{
    protected override void OnCreate()
    {
        base.OnCreate();
        Enabled = false; // Disable by default, enable when needed
    }
}