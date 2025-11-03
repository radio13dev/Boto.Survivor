using System;
using System.Collections.Generic;
using Drawing;
using MIConvexHull;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class MapDrawing5 : MonoBehaviourGizmos
{
    const int MAX_STAMPS_PER_SEGMENT = 10;

    public Transform[] Path = Array.Empty<Transform>();
    public float lineWidth;
    public int texturePaintTestSize = 10;
    
    public MeshRenderer TexRenderer;
    public RenderTexture EditTex;
    public Material BlitMat;
    public float PenStampDistance = 1f;
    public float2 PenStampDistanceScale = new float2(0.05f, 0.08f);
    
    [EditorButton]
    public void Clear()
    {
        RenderTexture.active = EditTex;
        GL.Clear(true,true,Color.clear);
        RenderTexture.active = null;
    }
    
    [EditorButton]
    public void Regenerate()
    {
        if (!TexRenderer || !EditTex || !BlitMat) return;
        if (Path.Length < 3)
            return;
        foreach (var path in Path)
            if (!path) return;
        
        var temp = RenderTexture.active;
        RenderTexture.active = EditTex;
        GL.Clear(true,true,Color.clear);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, EditTex.width, EditTex.height, 0);
        
        // We're going to use an Sdf texture to draw a line that follows the path
        for (int pathIt = 0; pathIt < Path.Length-1; pathIt++)
        {
            // 1. Get the current point and next point
            float2 a = TorusMapper.CartesianToToroidal(Path[pathIt].position);
            float2 b = TorusMapper.CartesianToToroidal(Path[pathIt+1].position);
            
            // 2. Path from a to b, with the number of steps based on the PenStampDistanceScale
            float dist = math.length((b-a)*PenStampDistanceScale);
            int steps = math.min((int)math.ceil(dist/PenStampDistance), MAX_STAMPS_PER_SEGMENT);
            for (int step = 0; step <= steps; step++)
            {
                float t = steps == 0 ? 0f : (float)step / (float)steps;
                float2 p = new float2(
                    math.frac((TorusMapper.Path.lerpangle(a.x, b.x, t) + math.PI2)/math.PI2), 
                    math.frac((TorusMapper.Path.lerpangle(a.y, b.y, t) + math.PI2)/math.PI2));
                float3 cartesianP = TorusMapper.ToroidalToCartesian(p, 0f);
                
                // 3. Convert p to texture space
                //int2 texPos = new int2(
                //    (int)math.clamp((p.x / TexSize.x) * EditTex.width, 0, EditTex.width - 1),
                //    (int)math.clamp((p.y / TexSize.y) * EditTex.height, 0, EditTex.height - 1)
                //);
                
                // 4. Adjust the SDF texture at texPos, wrapping around edges, by painting with a GL material
                BlitMat.SetVector("_TexturePosition", new float4(p,0,0));
                BlitMat.SetPass(0);
                
                // Use an 'additive' blend mode
                
                // Pass
                GL.Begin(GL.QUADS);
                GL.TexCoord2(0,0);
                GL.Vertex(new Vector3(0,0,0));
                GL.TexCoord2(1,0);
                GL.Vertex(new Vector3(EditTex.width,0,0));
                GL.TexCoord2(1,1);
                GL.Vertex(new Vector3(EditTex.width,EditTex.height,0));
                GL.TexCoord2(0,1);
                GL.Vertex(new Vector3(0,EditTex.height,0));
                GL.End();
            }
        }
        GL.PopMatrix();
        RenderTexture.active = temp;
    }
    private void Update()
    {
        Regenerate();
    }

    public override void DrawGizmos()
    {
        using (Draw.WithLineWidth(2, false))
        {
        }
    }
}