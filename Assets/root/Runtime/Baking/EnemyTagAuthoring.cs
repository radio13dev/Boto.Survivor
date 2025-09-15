using Unity.Entities;
using UnityEngine;

public class EnemyTagAuthoring : MonoBehaviour
{
    public Type type;

    partial class Baker : Baker<EnemyTagAuthoring>
    {
        public override void Bake(EnemyTagAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent<EnemyTag>(entity);

            switch (authoring.type)
            {
                case Type.EnemyTrapProjectile:
                    AddComponent<EnemyTrapProjectileAnimation>(entity);
                    AddComponent<MovementDisabled>(entity);
                    SetComponentEnabled<MovementDisabled>(entity, false);
                    break;
            }
        }
    }
    
    public enum Type
    {
        Generic,
        EnemyTrapProjectile
    }
}