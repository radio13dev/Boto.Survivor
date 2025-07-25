using Unity.Entities;
using UnityEngine;

public class BouncePhysicsAuthoring : MonoBehaviour
{
    public PhysicsResponse PhysicsResponse = new PhysicsResponse();
    
    partial class Baker : Baker<BouncePhysicsAuthoring>
    {
        public override void Bake(BouncePhysicsAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, authoring.PhysicsResponse);
            AddComponent<Movement>(entity);
            AddComponent(entity, new Grounded());
            SetComponentEnabled<Grounded>(entity, false);
            AddComponent<RotationalInertia>(entity);
            AddComponent<Force>(entity);
        }
    }
}