using NativeTrees;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class SurvivorAuthoring : MonoBehaviour
{
    public Movement Movement = new Movement(1,1,1);
    public bool EnableEnemySpawning;
    public bool EnableLaserProjectile;

    public partial class SurvivorBaker : Baker<SurvivorAuthoring>
    {
        public override void Bake(SurvivorAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddSharedComponent(entity, new PlayerControlled());
            AddComponent(entity, new CharacterTag());
            AddComponent(entity, new SurvivorTag());
            AddComponent(entity, new Health(100));
            AddComponent(entity, authoring.Movement);
            AddComponent(entity, new StepInput());
            AddComponent(entity, new MovementInputLockout());
            SetComponentEnabled<MovementInputLockout>(entity, false);
            AddComponent(entity, new ActiveLockout());
            SetComponentEnabled<ActiveLockout>(entity, false);
            AddComponent(entity, new Force());
            
            AddComponent(entity, new Collisions.Collider(new AABB2D(new float2(-1,-1), new float2(1,1))));
            
            if (authoring.EnableEnemySpawning) AddComponent(entity, new EnemySpawner(authoring.GetInstanceID()));
            
            AddBuffer<LinkedEntityGroup>(entity);
            
            AddComponent(entity, new ProjectileSpawner(Team.Survivor));
            SetComponentEnabled<ProjectileSpawner>(entity, false);
            
            AddComponent(entity, new LaserProjectileSpawner(Team.Survivor){Lifespan = 100});
            SetComponentEnabled<LaserProjectileSpawner>(entity, authoring.EnableLaserProjectile);
            
            AddComponent(entity, new RollActive());
            SetComponentEnabled<RollActive>(entity, false);
            AddComponent<SaveTag>(entity);
        }
    }
}