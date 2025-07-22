using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class CurrencyCollectableAuthoring : MonoBehaviour
{
    public MovementSettings MovementSettings = new MovementSettings(1);
    public PhysicsResponse PhysicsResponse = new PhysicsResponse();
    
    partial class Baker : Baker<CurrencyCollectableAuthoring>
    {
        public override void Bake(CurrencyCollectableAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            
            // Collectable setup
            AddComponent(entity, new Collectable());
            SetComponentEnabled<Collectable>(entity, false);
            
            // Physics (want it to bounce)
            AddComponent(entity, authoring.MovementSettings);
            AddComponent(entity, authoring.PhysicsResponse);
            AddComponent<Movement>(entity);
            AddComponent(entity, new Grounded());
            SetComponentEnabled<Grounded>(entity, false);
            AddComponent<RotationalInertia>(entity);
        }
    }
}

public struct Wallet : IComponentData
{
    public int Currency;
}

public struct CurrencyCollectable : IComponentData { }

[UpdateInGroup(typeof(CollectableSystemGroup))]
public partial struct CurrencyCollectableSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var collectable in SystemAPI.Query<RefRO<Collectable>>().WithAll<CurrencyCollectable>())
        {
            // Give currency to the entity
            if (SystemAPI.HasComponent<Wallet>(collectable.ValueRO.CollectedBy))
            {
                var walletRW = SystemAPI.GetComponentRW<Wallet>(collectable.ValueRO.CollectedBy);
                walletRW.ValueRW.Currency++;
            }
        }
    }
}