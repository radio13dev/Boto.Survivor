using BovineLabs.Saving;
using Unity.Entities;
using UnityEngine;

public class GemDropAuthoring : MonoBehaviour
{
    partial class Baker : Baker<GemDropAuthoring>
    {
        public override void Bake(GemDropAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent<GemDrop>(entity);
        }
    }
}
[Save]
public struct GemDrop : IComponentData
{
    public Gem Gem;
}