using System;
using NativeTrees;
using UnityEngine;
using Unity.Entities;

public class SurvivorAuthoring : MonoBehaviour
{
    public Movement Movement = new Movement(1,1,1);
    public bool EnableLaserProjectile;

    public partial class SurvivorBaker : Baker<SurvivorAuthoring>
    {
        public override void Bake(SurvivorAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent<LocalTransform2D>(entity); // save
            AddComponent(entity, new PlayerControlled(){ Index = -1 }); // save
            AddComponent(entity, new CharacterTag());
            AddComponent(entity, new SurvivorTag());
            AddComponent(entity, new Health(100)); // save
            AddComponent(entity, authoring.Movement); // save
            AddComponent(entity, new StepInput()); // save
            AddComponent(entity, new MovementInputLockout()); // save
            SetComponentEnabled<MovementInputLockout>(entity, false);
            AddComponent(entity, new ActiveLockout()); // save
            SetComponentEnabled<ActiveLockout>(entity, false);
            AddComponent(entity, new Force()); // save
            
            AddComponent(entity, new ProjectileSpawner(Team.Survivor));
            SetComponentEnabled<ProjectileSpawner>(entity, false); // save
            
            AddComponent(entity, new LaserProjectileSpawner(Team.Survivor){Lifespan = 5, TimeBetweenShots = 0.1});
            SetComponentEnabled<LaserProjectileSpawner>(entity, authoring.EnableLaserProjectile); // save
            
            AddComponent(entity, new RollActive());
            SetComponentEnabled<RollActive>(entity, false);
        }
    }
}