using Drawing;
using Unity.Collections;
using Unity.Entities;
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
            
            AddComponent(entity, new PlayerControlledSaveable(){ Index = byte.MaxValue });
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