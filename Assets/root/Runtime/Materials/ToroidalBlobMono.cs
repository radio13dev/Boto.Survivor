using System;
using System.Collections.Generic;
using Drawing;
using Unity.Mathematics;
using UnityEngine;

[ExecuteInEditMode]
public class ToroidalBlobMono : MonoBehaviourGizmos
{
    public float Radius;
    public byte Index;
    
    [NonSerialized] [HideInInspector] public List<(float3, byte)> m_Walls = new();
    [NonSerialized] [HideInInspector] public List<Vector3> m_WallsActual = new();
    
    public Metaball GetMetaball() => new Metaball()
    {
        Position = transform.position,
        RadiusSqr = Radius*Radius,
        Index = Index,
    };

    public override void DrawGizmos()
    {
        using (Draw.WithLineWidth(2, false))
        {
            Draw.Circle(transform.position, TorusMapper.GetNormal(transform.position), Radius, Color.HSVToRGB((math.cos(Index)+1)/2,1,1)*new Color(1, 1, 1, 0.4f));
            Draw.Polyline(m_WallsActual);
        }
    }

    private void OnValidate()
    {
        ToroidalBlobInit.SetDirty();
    }

    private void Update()
    {
        if (transform.hasChanged)
        {
            ToroidalBlobInit.SetDirty();
        }
    }
}
