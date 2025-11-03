using System;
using System.Collections.Generic;
using Drawing;
using Unity.Mathematics;
using UnityEngine;

[ExecuteInEditMode]
public class MapDrawing3 : MonoBehaviourGizmos
{
    public Transform[] Path = Array.Empty<Transform>();
    public float lineWidth;
    public float QuadAlpha = 1f;
    public float ArrowAlpha = 1f;
    public float LeftPathAlpha = 1f;
    public float RightPathAlpha = 1f;
    
    List<(List<Vector3> path, int group)> m_Quads = new();
    List<(float3 pos, float3 dir, string text, int group)> m_Arrows = new();
    List<Vector3> m_LeftPath = new();
    List<Vector3> m_RightPath = new();
    
    [EditorButton]
    public void Regenerate()
    {
        m_Quads.Clear();
        m_Arrows.Clear();
        m_LeftPath.Clear();
        m_RightPath.Clear();
    
        foreach (var path in Path)
            if (!path) return;
    
        for (int i = 0; i < Path.Length-2; i++)
        {
            float3 a = Path[i].position;
            float3 aN = TorusMapper.GetNormal(a);
            float3 b = Path[i+1].position;
            float3 bN = TorusMapper.GetNormal(b);
            float3 c = Path[i+2].position;
            float3 cN = TorusMapper.GetNormal(c);
            
            var ab = math.normalize(b-a);
            var abN = math.normalize((aN + bN)/2);
            var abR = math.cross(abN, ab);
            
            var bc = math.normalize(c-b);
            var bcN = math.normalize((bN + cN)/2);
            var bcR = math.cross(bcN, bc);
            
            var pL0 = a + abR * 0.5f * lineWidth;
            var pL1 = b + abR * 0.5f * lineWidth;
            var pL2 = b + bcR * 0.5f * lineWidth;
            var pL3 = c + bcR * 0.5f * lineWidth;
            
            var pR0 = a - abR * 0.5f * lineWidth;
            var pR1 = b - abR * 0.5f * lineWidth;
            var pR2 = b - bcR * 0.5f * lineWidth;
            var pR3 = c - bcR * 0.5f * lineWidth;
            
            var dotR = math.dot(ab, bcR);
            var dotR2 = math.dot(ab, bc);
            
            m_Quads.Add((new ()
            {
                pL0,
                pL1,
                pR1,
                pR0
            }, i));
            m_Arrows.Add((b, math.normalize(abR+bcR)*dotR, dotR.ToString("N2"), i));
            m_Arrows.Add((b, math.normalize(ab)*dotR2, dotR2.ToString("N2"), i+10));
            m_Arrows.Add((b, math.normalize((pR1+pR2)/2 - b)*(1 +math.clamp(-math.tan(math.asin(dotR2)), 0, 2))*(lineWidth/2), $"right est: {math.tan(math.asin(dotR2))}", i));
            m_Arrows.Add((b, math.normalize((pL1+pL2)/2 - b)*(1 +math.clamp(-math.tan(math.asin(dotR2)), 0, 2))*(lineWidth/2), $"left est: {math.tan(math.asin(dotR2))}", i));

            if (i == 0)
            {
                m_LeftPath.Add(pL0);
                m_RightPath.Add(pR0);
            }
            
            if (dotR > 0)
            {
                m_LeftPath.Add(pL1);
                m_LeftPath.Add(pL2);
                //m_RightPath.Add((pR1+pR2)/2);
                m_RightPath.Add(b + math.normalize((pR1+pR2)/2 - b)*(1 +math.clamp(-math.tan(math.asin(dotR2)), 0, 2))*(lineWidth/2));
            }
            else
            {
                //m_LeftPath.Add((pL1+pL2)/2);
                m_LeftPath.Add(b + math.normalize((pL1+pL2)/2 - b)*(1 +math.clamp(-math.tan(math.asin(dotR2)), 0, 2))*(lineWidth/2));
                m_RightPath.Add(pR1);
                m_RightPath.Add(pR2);
            }
            
            if (i == Path.Length-3)
            {
                m_Quads.Add((new ()
                {
                    pL2,
                    pL3,
                    pR3,
                    pR2
                }, i+1));
                
                
                if (dotR > 0)
                {
                    m_LeftPath.Add(pL2);
                }
                else
                {
                    m_RightPath.Add(pR2);
                }
                m_LeftPath.Add(pL3);
                m_RightPath.Add(pR3);
            }
            
        }
    }

    private void Update()
    {
        Regenerate();
    }

    public override void DrawGizmos()
    {
        using (Draw.WithLineWidth(2, false))
        {
            foreach (var quad in m_Quads)
            {
                Draw.Polyline(quad.path, cycle: true, Color.HSVToRGB(quad.group*0.23f%1f, .8f, 1f)*new Color(1,1,1,QuadAlpha));
            }
            foreach (var arrow in m_Arrows)
            {
                var c = Color.HSVToRGB(arrow.group*0.23f%1, .8f, 1)*new Color(1,1,1,ArrowAlpha);
                Draw.Arrow(arrow.pos, arrow.pos + arrow.dir, c);
                Draw.Label2D(arrow.pos + arrow.dir*1.2f, arrow.text, 14, LabelAlignment.Center, c);
            }
            Draw.Polyline(m_LeftPath, Color.gray*new Color(1,1,1,LeftPathAlpha));
            Draw.Polyline(m_RightPath, Color.beige*new Color(1,1,1,RightPathAlpha));
        }
    }
}