using Unity.Entities;
using UnityEngine;

public class RingItemAuthoring : MonoBehaviour
{
    partial class Baker : Baker<RingItemAuthoring>
    {
        public override void Bake(RingItemAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.WorldSpace);
            AddComponent(entity, new RingStats() { PrimaryEffect = RingPrimaryEffect.Projectile_Ring });
        }
    }
}