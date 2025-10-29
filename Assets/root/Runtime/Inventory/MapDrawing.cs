using System;
using System.Collections.Generic;
using Drawing;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteInEditMode]
public class MapDrawing : MonoBehaviour
{
    public List<Vector3> Path = new();
    public List<Vector3> PathNormals = new();
    public List<float3> Nodes = new();
    public List<float2> PointsToroidal = new();
    List<Vector3> m_PathOutline_GeneratedPerFrame = new();
    
    public int SEGMENT_SIZE = 10;
    public float FloorOffset = 0.5f;
    public float OutlineOffset = 0.1f;
    public bool AutoSegmentSize = true;
    public float2 AutoSegmentSpacing = new float2(0.1f, 0.3f);
    public float2 SketchSpacing = new float2(0.05f, 0.05f);
    
    
    // Game settings
    [Header("Game Settings")]
    public float Game_LineWidth = 2;
    public Color Game_LineColor = Color.white;
    public float Game_CircleRadius = 4;
    public Color Game_CircleColor = Color.white;
    public float Game_OutlineWidth = 2;
    public Color Game_OutlineColor = Color.white;
    public bool Game_ConnectLines = true;
    
    // Map settings
    [Header("Map Settings")]
    public float Map_LineWidth = 2;
    public Color Map_LineColor = Color.white;
    public float Map_CircleRadius = 4;
    public Color Map_CircleColor = Color.white;
    public float Map_OutlineWidth = 2;
    public Color Map_OutlineColor = Color.white;
    public bool Map_ConnectLines = true;
    
    public void Clear()
    {
        m_HasSketch = false;
        Path.Clear();
        PathNormals.Clear();
        Nodes.Clear();
        PointsToroidal.Clear();
    }

    private void OnValidate()
    {
        RegeneratePoints();
    }
    
    public void RegeneratePoints()
    {
        // Remember old nodes
        var oldPoints = new List<float3>(Nodes);
    
        // Clear
        Clear();
        
        // Add all toroidal points back in again
        for (int i = 0; i < oldPoints.Count; i++)
            AddPoint(oldPoints[i]);
    }
    
    [EditorButton]
    private void Demo()
    {
        Clear();
        AddPoint(math.float3(100, 100, 0));
        AddPoint(math.float3(100, 100, 10));
        AddPoint(math.float3(120, 100, 20));
        AddPoint(math.float3(140, 50, 40));
        AddPoint(math.float3(100, 20, 0));
        AddPoint(math.float3(10, 100, 100));
        AddPoint(math.float3(10, -100, -100));
    }
    
    bool m_HasSketch = false;
    public void AddSketchPoint(float3 point)
    {
        // Only add this to the path IF it's different enough from the last point
        if (Nodes.Count == 0)
        {
            AddPoint(point);
            return;
        }
            
        var comp = m_HasSketch ? Nodes[^2] : Nodes[^1];
        if (math.any(math.abs(TorusMapper.Path.deltaAngle(TorusMapper.CartesianToToroidal(comp), TorusMapper.CartesianToToroidal(point))) > SketchSpacing))
        {
            AddPoint(point);
        }
        else
        {
            if (m_HasSketch)
            {
                Path[^1] = point;
                PathNormals[^1] = TorusMapper.GetNormal(point);
                Nodes[^1] = point;
            }
            else
            {
                m_HasSketch = true;
                Path.Add(point);
                PathNormals.Add(TorusMapper.GetNormal(point));
                Nodes.Add(point);
            }
        }
    }
    
    public void EndSketchPoint()
    {
        if (m_HasSketch)
            AddPoint(Nodes[^1]);
    }

    public void AddPoint(float3 point)
    {
        if (m_HasSketch)
        {
            Path.RemoveAt(Path.Count-1);
            PathNormals.RemoveAt(PathNormals.Count-1);
            Nodes.RemoveAt(Nodes.Count-1);
            m_HasSketch = false;
        }
        
        PointsToroidal.Add(TorusMapper.CartesianToToroidal(point));
        Nodes.Add(TorusMapper.ToroidalToCartesian(PointsToroidal[^1]));
        
        // Draw a path from the last point to this point
        if (PointsToroidal.Count > 1)
        {
            var path = new TorusMapper.Path(PointsToroidal[^2], PointsToroidal[^1]);
            
            int sectionSize = AutoSegmentSize ? 1 + (int)(math.length(math.abs(TorusMapper.Path.deltaAngle(path.AT, path.BT)/AutoSegmentSpacing))) : SEGMENT_SIZE;
            sectionSize = math.min(sectionSize, 30);
            for (int i = 1; i < sectionSize; i++)
            {
                // Add the inbetween points
                var t = (float)i / sectionSize;
                var cartesianPoint = path.Evaluate(t);
                var normal = TorusMapper.GetNormal(cartesianPoint);
                cartesianPoint += normal*FloorOffset;
                Path.Add(cartesianPoint);
                PathNormals.Add(normal);
            }
        }

        // Always add the node point to the end of the path
        {
            var cartesianPoint = Nodes[^1];
            var normal = TorusMapper.GetNormal(cartesianPoint);
            cartesianPoint += normal*FloorOffset;
            Path.Add(cartesianPoint);
            PathNormals.Add(normal);
        }
    }

    private void Update()
    {
        Redraw();
    }

    private void Redraw()
    {
        DrawingManager.allowRenderToRenderTextures = true;
        
        if (m_PathOutline_GeneratedPerFrame.Count > Path.Count)
        {
            m_PathOutline_GeneratedPerFrame.RemoveRange(Path.Count-1, m_PathOutline_GeneratedPerFrame.Count - Path.Count);
        }
        else if (m_PathOutline_GeneratedPerFrame.Count < Path.Count)
        {
            for (int i = m_PathOutline_GeneratedPerFrame.Count; i < Path.Count; i++)
                m_PathOutline_GeneratedPerFrame.Add(Vector3.zero);
        }
            
        {
            var draw = DrawingManager.GetBuilder(renderInGame: true);
            draw.cameraTargets = new Camera[]{ CameraRegistry.Map };
            // Outline
            using (draw.WithLineWidth(Map_OutlineWidth, Map_ConnectLines))
            {
                var cameraForward = CameraRegistry.Map.transform.forward;
                for (int i = 0; i < m_PathOutline_GeneratedPerFrame.Count; i++)
                {
                    m_PathOutline_GeneratedPerFrame[i] = Path[i] + cameraForward * OutlineOffset;
                }
                    
                draw.CatmullRom(m_PathOutline_GeneratedPerFrame, Map_OutlineColor);
            }
            // Fill
            using (draw.WithLineWidth(Map_LineWidth, Map_ConnectLines))
            {
                draw.CatmullRom(Path, Map_LineColor);
                for (int i = 0; i < Path.Count; i++)
                    draw.SphereOutline(Path[i], Map_CircleRadius);
            }
            draw.Dispose();
        }
        
        
        {
            var draw = DrawingManager.GetBuilder(renderInGame: true);
            if (Application.isPlaying) draw.cameraTargets = new Camera[]{ CameraRegistry.Main };
            using (draw.WithLineWidth(Game_OutlineWidth, Map_ConnectLines))
            {
                var cameraForward = CameraRegistry.Main.transform.forward;
                for (int i = 0; i < m_PathOutline_GeneratedPerFrame.Count; i++)
                {
                    
                    m_PathOutline_GeneratedPerFrame[i] = Path[i] + cameraForward * OutlineOffset;
                }

                draw.CatmullRom(m_PathOutline_GeneratedPerFrame, Game_OutlineColor);
            }
            using (draw.WithLineWidth(Game_LineWidth, Game_ConnectLines))
            {
                draw.CatmullRom(Path, Game_LineColor);
                for (int i = 0; i < Path.Count; i++)
                    draw.SphereOutline(Path[i], Game_CircleRadius);
            }
            draw.Dispose();
        }
    }
}