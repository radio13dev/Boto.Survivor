using System;
using UnityEngine;

public class TorusTerrainTool : MonoBehaviour
{
    public MeshFilter MeshFilter;
    Mesh m_Mesh;
    bool m_GeneratedMesh;
    
    [EditorButton]
    public void GenerateMesh(float radius, float thickness, int ringSegments, int tubeSegments)
    {
        if (!m_GeneratedMesh)
        {
            m_Mesh = new Mesh();
            m_GeneratedMesh = true;
        }
        
        TorusMeshGenerator.GenerateTorusMesh(radius, thickness, ringSegments, tubeSegments, TorusMeshGenerator.Axis.y, out var verts, out var tris, out var normals, out var uvs);
        m_Mesh.SetVertices(Array.ConvertAll(verts, v => (Vector3)v));
        m_Mesh.SetTriangles(tris, 0);
        m_Mesh.SetNormals(Array.ConvertAll(normals, v => (Vector3)v));
        m_Mesh.SetUVs(0, Array.ConvertAll(uvs, v => (Vector2)v));
        
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