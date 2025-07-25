using Unity.Entities;
using UnityEngine;

public class LootGenerator2Authoring : MonoBehaviour
{
    partial class Baker : Baker<LootGenerator2Authoring>
    {
        public override void Bake(LootGenerator2Authoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.WorldSpace);
            AddComponent(entity, new LootGenerator2()
            {
                OptionCount = 3
            });
        }
    }
}