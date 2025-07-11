using NativeTrees;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class SurvivorAuthoring : MonoBehaviour
{
    public partial class SurvivorBaker : Baker<SurvivorAuthoring>
    {
        public override void Bake(SurvivorAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new CharacterTag());
            AddComponent(entity, new SurvivorTag());
            AddComponent(entity, new Health(100));
            AddComponent(entity, new Movement(10, 10, 10));
            AddComponent(entity, new StepInput());
            AddComponent(entity, new MovementInputLockout());
            SetComponentEnabled<MovementInputLockout>(entity, false);
            AddComponent(entity, new ActiveLockout());
            SetComponentEnabled<ActiveLockout>(entity, false);
            AddComponent<PlayerControlledTag>(entity);
            
            AddComponent(entity, new Collisions.Collider(new AABB2D(new float2(-1,-1), new float2(1,1))));
            
            AddBuffer<LinkedEntityGroup>(entity);
            
            AddComponent(entity, new ProjectileSpawner(Team.Survivor));
            SetComponentEnabled<ProjectileSpawner>(entity, false);
            
            AddComponent(entity, new LaserProjectileSpawner(Team.Survivor));
            SetComponentEnabled<LaserProjectileSpawner>(entity, true);
            
            AddComponent(entity, new RollActive());
            SetComponentEnabled<RollActive>(entity, false);
        }
    }
}