using Drawing;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class DeadSurvivorAuthoring : MonoBehaviourGizmos
{
    partial class Baker : Baker<DeadSurvivorAuthoring>
    {
        public override void Bake(DeadSurvivorAuthoring authoring)
        {
            if (!DependsOn(GetComponent<StepInputAuthoring>()))
            {
                Debug.LogError($"Cannot bake {authoring.gameObject}. Requires component: {nameof(StepInputAuthoring)}");
                return;
            }
            
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            
            // Basic setup
            AddComponent(entity, new PlayerControlledSaveable(){ Index = byte.MaxValue });
            AddComponent(entity, new DeadSurvivorTag());
            
            // Stats to track for reviving
            AddComponent(entity, new Wallet(){ Value = 0 });
            AddComponent(entity, TiledStatsTree.Default);
            
            // Rings and inventory
            var rings = AddBuffer<Ring>(entity);
            rings.Resize(Ring.k_RingCount, NativeArrayOptions.ClearMemory);
            var equippedGems = AddBuffer<EquippedGem>(entity);
            equippedGems.Resize(Ring.k_RingCount*Gem.k_GemsPerRing, NativeArrayOptions.ClearMemory);
            var inventoryGems = AddBuffer<InventoryGem>(entity);
        }
    }
}

public partial struct DeadSurvivorReviveSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (circlable, transform, e) in SystemAPI.Query<RefRO<Circlable>, RefRO<LocalTransform>>().WithEntityAccess().WithAll<DeadSurvivorTag>())
        {
            if (circlable.ValueRO.Charge >= circlable.ValueRO.MaxCharge)
            {
                // Destroy this
                SystemAPI.SetComponentEnabled<DestroyFlag>(e, true);
            }
        }
    }
}