using NativeTrees;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class SurvivorAuthoring : MonoBehaviour
{
    public partial class SurvivorBaker : Baker<SurvivorAuthoring>
    {
        public override void Bake(SurvivorAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new CharacterTag());
            AddComponent(entity, new SurvivorTag());
            AddComponent(entity, new Movement(1, 1));
            AddComponent(entity, new PlayerInput());
            AddComponent(entity, new ProjectileSpawner());
            
            AddComponent(entity, new Collider(new AABB2D(new float2(-1,-1), new float2(1,1))));
            SetComponentEnabled<Collider>(entity, false);
        }
    }
}