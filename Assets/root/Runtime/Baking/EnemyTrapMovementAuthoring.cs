using Unity.Entities;
using UnityEngine;


public struct EnemyTrapMovement : IComponentData
{
    public enum States : byte
    {
        Idle,
        Charge,
    }

    public States state;
    public byte target;
    public float timer;
    public NetworkId ProjectileNetworkId;
}

public class EnemyTrapMovementAuthoring : MonoBehaviour
{
    public class EnemyTrapMovementBaker : Baker<EnemyTrapMovementAuthoring>
    {
        public override void Bake(EnemyTrapMovementAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<EnemyTrapMovement>(entity);
            AddComponent<MovementInputLockout>(entity);
            SetComponentEnabled<MovementInputLockout>(entity, false);
        }
    }
}