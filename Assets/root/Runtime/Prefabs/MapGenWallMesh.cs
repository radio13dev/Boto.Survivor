using Drawing;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class MapGenWallMesh : MonoBehaviourGizmos
{
    public float RadiusMod = 1f;
    public MeshRenderer MeshRenderer;
    public float Radius => RadiusMod*transform.lossyScale.x;
    public override void DrawGizmos()
    {
        if (!GizmoContext.InSelection(this)) return;
        
        using (Draw.WithLineWidth(2, false))
        {
            Draw.Circle(transform.position, TorusMapper.GetNormal(transform.position), Radius, Color.yellow*new Color(1, 1, 1, 0.4f));
        }
    }
}