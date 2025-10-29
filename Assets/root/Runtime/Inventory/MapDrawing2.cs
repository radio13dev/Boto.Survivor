using System;
using System.Collections.Generic;
using Drawing;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteInEditMode]
public class MapDrawing2 : MonoBehaviour
{
    public List<float3> Path = new();
    public List<float3> PathNormals = new();
    public List<float3> Nodes = new();
    public List<float2> PointsToroidal = new();
    
    Mesh GetCreateMesh() {
        if (!m_Mesh)
        {
            const int MAX_POINTS = 1000;
            
            m_Mesh = new Mesh();
            m_VertexArray = new (MAX_POINTS*2, Allocator.Persistent);
            m_NormalArray = new (MAX_POINTS*2, Allocator.Persistent);
            m_TangentArray = new (MAX_POINTS*2, Allocator.Persistent);
            m_IndexArray = new ((MAX_POINTS-1)*6, Allocator.Persistent);
        }
        return m_Mesh;
    }
    Mesh m_Mesh;
    NativeArray<float3> m_VertexArray;
    NativeArray<float3> m_NormalArray;
    NativeArray<float4> m_TangentArray;
    NativeArray<int> m_IndexArray;
    bool m_Dirty;
    
    public MeshFilter LineMeshFilter;
    public MeshFilter OutlineMeshFilter;
    
    public int SEGMENT_SIZE = 10;
    public float FloorOffset = 0.5f;
    public float OutlineOffset = 0.1f;
    public bool AutoSegmentSize = true;
    public float2 AutoSegmentSpacing = new float2(0.1f, 0.3f);
    public float2 SketchSpacing = new float2(0.05f, 0.05f);
    
    
    // Game settings
    [Header("Game Settings")]
    public float LineWidth = 2;
    
    private void OnValidate()
    {
        RegeneratePoints();
    }
    
    private void LateUpdate()
    {
        if (m_Dirty)
            RegenerateMesh();
    }

    private void OnDestroy()
    {
        if (m_Mesh) Destroy(m_Mesh);
        if (m_VertexArray.IsCreated) m_VertexArray.Dispose();
        if (m_NormalArray.IsCreated) m_NormalArray.Dispose();
        if (m_TangentArray.IsCreated) m_TangentArray.Dispose();
        if (m_IndexArray.IsCreated) m_IndexArray.Dispose();
    }

    public void Clear()
    {
        m_HasSketch = false;
        
        Path.Clear();
        PathNormals.Clear();
        Nodes.Clear();
        PointsToroidal.Clear();
        
        m_Dirty = true;
    }

    public void RegenerateMesh()
    {
        m_Dirty = false;
        
        var mesh = GetCreateMesh();
        var vertices = m_VertexArray;
        var normals = m_NormalArray;
        var tangents = m_TangentArray;
        var indices = m_IndexArray;
        
        int pointCount = math.min(Path.Count, m_VertexArray.Length/2);
        for (int i = 0; i < pointCount; i++)
        {
            // Fill the mesh with a line along the path, using LineWidth as the width
            var right = math.cross(math.normalizesafe(Path[i+1]-Path[i]) , PathNormals[i]);
            vertices[i] = Path[i] - right*LineWidth;
            vertices[i+1] = Path[i] + right*LineWidth;
            
            normals[i] = PathNormals[i];
            normals[i+1] = PathNormals[i];
            
            tangents[i] = new float4(-right, 1);
            tangents[i+1] = new float4(right, 1);
            
            if (i != 0)
            {
                indices[i*6 + 0] = (i-1)*2 + 0;
                indices[i*6 + 1] = (i-1)*2 + 1;
                indices[i*6 + 2] = (i)*2 + 0;
                indices[i*6 + 3] = (i-1)*2 + 1;
                indices[i*6 + 4] = (i)*2 + 1;
                indices[i*6 + 5] = (i)*2 + 0;
            }
        }
        
        mesh.SetVertices(m_VertexArray, 0, pointCount*2);
        mesh.SetNormals(m_NormalArray, 0, pointCount*2);
        mesh.SetTangents(m_TangentArray, 0, pointCount*2);
        mesh.SetIndices(m_IndexArray, 0, math.max((pointCount-1)*6, 0));
        
        LineMeshFilter.sharedMesh = mesh;
        OutlineMeshFilter.sharedMesh = mesh;
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
            m_Dirty = true;
        }
    }
    
    public void EndSketchPoint()
    {
        if (m_HasSketch)
            AddPoint(Nodes[^1]);
    }

    public void AddPoint(float3 point)
    {
        m_Dirty = true;
        
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
}