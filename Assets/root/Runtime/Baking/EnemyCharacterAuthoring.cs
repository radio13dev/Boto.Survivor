using System;
using UnityEngine;
using Unity.Entities;

public class EnemyCharacterAuthoring : MonoBehaviour
{
    public Movement Movement = new Movement(1,1,1);
    
    public partial class SurvivorBaker : Baker<EnemyCharacterAuthoring>
    {
        public override void Bake(EnemyCharacterAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new CharacterTag());
            AddComponent(entity, new EnemyTag());
            AddComponent(entity, new Health(100));
            AddComponent(entity, authoring.Movement);
            AddComponent(entity, new StepInput());
            AddComponent(entity, new Force());
            
            
            AddBuffer<LinkedEntityGroup>(entity);
            AddComponent<SaveTag>(entity);
        }
    }
}