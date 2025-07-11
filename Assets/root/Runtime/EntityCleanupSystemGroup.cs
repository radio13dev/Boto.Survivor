using Unity.Entities;

/// <summary>
/// Group in which entities are deleted/cleaned up. Ordered last in the prediction step to ensure other systems don't access these entities.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class EntityCleanupSystemGroup : ComponentSystemGroup
{
        
}