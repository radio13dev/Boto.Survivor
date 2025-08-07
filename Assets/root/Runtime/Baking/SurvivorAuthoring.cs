using BovineLabs.Saving;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public struct PlayerControlled : ISharedComponentData
{
    public int Index;
}

[Save]
public struct PlayerControlledSaveable : IComponentData
{
    public int Index;
}


public class SurvivorAuthoring : MonoBehaviour
{
    public MovementSettings MovementSettings = new MovementSettings();
    public PhysicsResponse PhysicsResponse = new PhysicsResponse();
    public Health Health = new Health(100);
    public RingStats[] InitialRings = new RingStats[8];

    partial class Baker : Baker<SurvivorAuthoring>
    {
        public override void Bake(SurvivorAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            
            // Basic character setup
            AddComponent(entity, new PlayerControlledSaveable(){ Index = -1 });
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
            rings.Resize(Ring.k_RingCount, NativeArrayOptions.ClearMemory);
            var equippedGems = AddBuffer<EquippedGem>(entity);
            equippedGems.Resize(Ring.k_RingCount*Gem.k_GemsPerRing, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < rings.Length && i < authoring.InitialRings.Length; i++)
                rings[i] = new Ring(){ Stats = authoring.InitialRings[i] };
            
            var inventoryGems = AddBuffer<InventoryGem>(entity);
            inventoryGems.Add(new InventoryGem(new Gem(){ GemType = Gem.Type.Multishot }));
            inventoryGems.Add(new InventoryGem(new Gem(){ GemType = Gem.Type.Multishot }));
            inventoryGems.Add(new InventoryGem(new Gem(){ GemType = Gem.Type.Multishot }));
        }
    }
}

[UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
public partial struct PlayerControlledSystem : ISystem
{
    EntityQuery m_Query;
    public void OnCreate(ref SystemState state)
    {
        m_Query = SystemAPI.QueryBuilder().WithAll<PlayerControlledSaveable>().WithNone<PlayerControlled>().Build();
        state.RequireForUpdate(m_Query);
    }

    public void OnUpdate(ref SystemState state)
    {
        using var entities = m_Query.ToEntityArray(Allocator.Temp);
        using var ids = m_Query.ToComponentDataArray<PlayerControlledSaveable>(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
            state.EntityManager.AddSharedComponent(entities[i], new PlayerControlled(){ Index = ids[i].Index });
    }
}