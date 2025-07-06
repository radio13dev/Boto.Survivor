using Unity.Entities;
using Unity.NetCode;

/// <summary>
/// Projectile collision is predicted, only after all movement is done
/// </summary>
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
public partial class GameLogicSystemGroup : ComponentSystemGroup
{
        
}