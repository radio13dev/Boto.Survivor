using System;
using Drawing;
using UnityEngine;

[ExecuteInEditMode]
public class ToroidalBlobMono : MonoBehaviourGizmos
{
    public float Radius;
    public int Index;

    public override void DrawGizmos()
    {
        using (Draw.WithLineWidth(2, false))
        {
            Draw.WireSphere(transform.position, Radius, Color.HSVToRGB(Index,1,1));
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
