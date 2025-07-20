using NativeTrees;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class PickupAuthoring : MonoBehaviour
{
    public partial class SurvivorBaker : Baker<PickupAuthoring>
    {
        public override void Bake(PickupAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new Pickup());
            AddComponent(entity, new DestroyAtTime(){ DestroyTime = double.MaxValue });
        }
    }
}