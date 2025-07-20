using System;
using UnityEngine;
using Unity.Entities;

public class EnemyCharacterAuthoring : MonoBehaviour
{
    public Movement Movement = new Movement(1,1,1);
    public Health Health = new Health(10);
    
    public partial class SurvivorBaker : Baker<EnemyCharacterAuthoring>
    {
        public override void Bake(EnemyCharacterAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent<LocalTransform2D>(entity);
            AddComponent<CharacterTag>(entity);
            AddComponent<EnemyTag>(entity);
            
            AddComponent(entity, new StepInput());
            AddComponent(entity, new Force());
            
            AddComponent(entity, authoring.Health);
            AddComponent(entity, authoring.Movement);
        }
    }
}