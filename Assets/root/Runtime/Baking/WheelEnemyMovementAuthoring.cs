using Unity.Entities;
using UnityEngine;


public struct WheelEnemyMovement : IComponentData
{
    public byte state;
    public byte target;
    public float timer;
}

public class WheelEnemyMovementAuthoring : MonoBehaviour
{
    public class WheelEnemyMovementBaker : Baker<WheelEnemyMovementAuthoring>
    {
        public override void Bake(WheelEnemyMovementAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<WheelEnemyMovement>(entity);
            AddComponent<MovementInputLockout>(entity);
            SetComponentEnabled<MovementInputLockout>(entity, false);
        }
    }
}