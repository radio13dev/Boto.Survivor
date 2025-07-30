using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class SurvivorAuthoring : MonoBehaviour
{
    public MovementSettings MovementSettings = new MovementSettings();
    public PhysicsResponse PhysicsResponse = new PhysicsResponse();
    public Health Health = new Health(100);
    public bool EnableLaserProjectile;

    partial class Baker : Baker<SurvivorAuthoring>
    {
        public override void Bake(SurvivorAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            
            // Basic character setup
            AddComponent(entity, new PlayerControlled(){ Index = -1 });
            AddComponent(entity, new CharacterTag());
            AddComponent(entity, new SurvivorTag());
            AddComponent(entity, authoring.Health);
            
            // Movement and Inputs
            AddComponent(entity, authoring.MovementSettings);
            AddComponent<Movement>(entity);
            AddComponent<Grounded>(entity);
            SetComponentEnabled<Grounded>(entity, false);
            AddComponent(entity, authoring.PhysicsResponse);
            AddComponent<RotateWithSurface>(entity);
            AddComponent<StepInput>(entity);
            AddComponent<Force>(entity);
            
            // Abilities and Input Lockouts
            AddComponent(entity, new MovementInputLockout());
            SetComponentEnabled<MovementInputLockout>(entity, false);
            AddComponent(entity, new ActiveLockout());
            SetComponentEnabled<ActiveLockout>(entity, false);
            AddComponent(entity, new RollActive());
            SetComponentEnabled<RollActive>(entity, false);
            
            // Stats
            AddComponent(entity, new CompiledStats());
            AddComponent(entity, new CompiledStatsDirty());
            var rings = AddBuffer<Ring>(entity);
            rings.Resize(8, NativeArrayOptions.ClearMemory);
            if (authoring.EnableLaserProjectile) rings[0] = new Ring(){ Stats = new RingStats(){ PrimaryEffect = RingPrimaryEffect.Projectile_Ring } };
            
        }
    }
}