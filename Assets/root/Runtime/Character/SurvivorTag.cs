using Unity.Entities;
using Unity.NetCode;

[GhostComponent]
public struct SurvivorTag : IComponentData { }

[GhostComponent]
public struct Health : IComponentData
{
    public int Value;
}