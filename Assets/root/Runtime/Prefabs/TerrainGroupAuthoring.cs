using System;
using BovineLabs.Saving;
using Drawing;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class TerrainGroupAuthoring : MonoBehaviourGizmos
{
    public TerrainGroup TerrainGroup;

    partial class Baker : Baker<TerrainGroupAuthoring>
    {
        public override void Bake(TerrainGroupAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, authoring.TerrainGroup);
        }
    }

    public override void DrawGizmos()
    {
        var draw = Draw.editor;
        draw.WireSphere(transform.position, TerrainGroup.ExclusionRadius, new Color(1, 1f, 1f, 0.5f));
    }
}

[Serializable]
[Save]
public struct TerrainGroup : IComponentData
{
    public float ExclusionRadius;
}