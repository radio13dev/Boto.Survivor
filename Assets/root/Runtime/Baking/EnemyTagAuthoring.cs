using Unity.Entities;
using UnityEngine;

public class EnemyTagAuthoring : MonoBehaviour
{
    partial class Baker : Baker<EnemyTagAuthoring>
    {
        public override void Bake(EnemyTagAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent<EnemyTag>(entity);
        }
    }
}