using Unity.Entities;
using Unity.NetCode;

[GhostComponent]
public struct CharacterTag : IComponentData { }
[GhostComponent]
public struct EnemyTag : IComponentData { }
[GhostComponent]
public struct SurvivorTag : IComponentData { }

[GhostComponent]
public struct Health : IComponentData
{
    public int Value;
    
    public Health(int health)
    {
        Value = health;
    }
}