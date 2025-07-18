using System;
using UnityEngine;

public class TorusTerrainTool : MonoBehaviour
{
    public MeshFilter MeshFilter;
    Mesh m_Mesh;
    bool m_GeneratedMesh;
    
    public float Radius = 20;
    public float Thickness = 5;
    public int RingSegments = 5;
    public int TubeSegments = 5;
    
    [EditorButton]
    public void GenerateMeshDefault()
    {
        GenerateMesh(Radius, Thickness, RingSegments, RingSegments);
    }
    
    [EditorButton]
    public void GenerateMesh(float radius, float thickness, int ringSegments, int tubeSegments)
    {
        if (MeshFilter.sharedMesh)
            m_Mesh = MeshFilter.sharedMesh;
        else
        {
            m_Mesh = new Mesh();
            m_GeneratedMesh = true;
        }
        
        TorusMeshGenerator.GenerateTorusMesh(radius, thickness, ringSegments, tubeSegments, out var verts, out var tris, out var normals);
        m_Mesh.SetVertices(Array.ConvertAll(verts, v => (Vector3)v));
        m_Mesh.SetTriangles(tris, 0);
        m_Mesh.SetNormals(Array.ConvertAll(normals, v => (Vector3)v));
        
        MeshFilter.sharedMesh = m_Mesh;
    }

    private void OnDestroy()
    {
        if (m_GeneratedMesh && m_Mesh)
        {
            if (MeshFilter && MeshFilter.sharedMesh == m_Mesh) MeshFilter.sharedMesh = null;
            Destroy(m_Mesh);
        }
    }
}