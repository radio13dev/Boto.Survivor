using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using AABB = NativeTrees.AABB;
using Collider = Collisions.Collider;

public class CurrencyCollectableAuthoring : MonoBehaviour
{
    public float3 CollectableMin = new float3(-0.5f,-0.5f,-0.5f);
    public float3 CollectableMax = new float3(0.5f,0.5f,0.5f);
    
    partial class Baker : Baker<CurrencyCollectableAuthoring>
    {
        public override void Bake(CurrencyCollectableAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            
            // Collectable setup
            AddComponent(entity, new Collectable());
            SetComponentEnabled<Collectable>(entity, false);
            AddComponent(entity, new Collected());
            SetComponentEnabled<Collected>(entity, false);
            AddComponent(entity, new CollectCollider(){Collider = new Collider(new AABB(authoring.CollectableMin, authoring.CollectableMax))});
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.5f, 0f, 0.3f, 1f);
        Gizmos.DrawWireCube(transform.position + (Vector3)(CollectableMin/2 + CollectableMax/2), (CollectableMax - CollectableMin));
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
        foreach (var collectable in SystemAPI.Query<RefRO<Collectable>>().WithAll<Collected, CurrencyCollectable>())
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