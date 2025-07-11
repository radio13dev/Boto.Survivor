using NativeTrees;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class EnemyCharacterAuthoring : MonoBehaviour
{
    public partial class SurvivorBaker : Baker<EnemyCharacterAuthoring>
    {
        public override void Bake(EnemyCharacterAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new CharacterTag());
            AddComponent(entity, new EnemyTag());
            AddComponent(entity, new Health(100));
            AddComponent(entity, new Movement(10, 10, 10));
            AddComponent(entity, new StepInput());
            
            AddComponent(entity, new Collisions.Collider(new AABB2D(new float2(-1,-1), new float2(1,1))));
            
            AddBuffer<LinkedEntityGroup>(entity);
        }
    }
}