using UnityEngine;
using Unity.Entities;

public class EnemyCharacterAuthoring : MonoBehaviour
{
    public Health Health = new Health(10);
    
    [Range(0,100)]
    public int Chance;
    
    partial class Baker : Baker<EnemyCharacterAuthoring>
    {
        public override void Bake(EnemyCharacterAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            
            // Basic character setup
            AddComponent<CharacterTag>(entity);
            AddComponent<EnemyTag>(entity);
            AddComponent(entity, authoring.Health);
        }
    }
}