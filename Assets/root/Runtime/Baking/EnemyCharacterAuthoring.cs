using System;
using UnityEngine;
using Unity.Entities;

public class EnemyCharacterAuthoring : MonoBehaviour
{
    public MovementSettings MovementSettings = new MovementSettings(1);
    public PhysicsResponse PhysicsResponse = new PhysicsResponse();
    public Health Health = new Health(10);
    
    partial class Baker : Baker<EnemyCharacterAuthoring>
    {
        public override void Bake(EnemyCharacterAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            
            // Basic character setup
            AddComponent<CharacterTag>(entity);
            AddComponent<EnemyTag>(entity);
            AddComponent(entity, authoring.Health);
            
            // Movement and Inputs
            AddComponent(entity, authoring.MovementSettings);
            AddComponent<Movement>(entity);
            AddComponent(entity, authoring.PhysicsResponse);
            AddComponent<RotateWithSurface>(entity);
            AddComponent<Grounded>(entity);
            AddComponent<StepInput>(entity);
            AddComponent<LastStepInputLastDirection>(entity);
            AddComponent<Force>(entity);
            
        }
    }
}