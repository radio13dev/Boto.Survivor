using Unity.Entities;

public struct CharacterTag : IComponentData { }
public struct EnemyTag : IComponentData { }
public struct SurvivorTag : IComponentData { }

public struct Health : IComponentData
{
    public int Value;
    
    public Health(int health)
    {
        Value = health;
    }
}