using System;
using UnityEngine;

public class MeshCreator : MonoBehaviour
{
    Mesh m_Mesh;
    bool m_GeneratedMesh;
    
    [EditorButton]
    public void GenerateMesh()
    {
        if (!TryGetComponent<MeshFilter>(out var meshFilter)) return;
    
        if (!m_GeneratedMesh)
        {
            m_Mesh = new Mesh();
            m_GeneratedMesh = true;
        }
        
        QuadMeshGenerator.GenerateQuadMesh(1, 1, QuadMeshGenerator.Axis.y, out var verts, out var tris, out var normals, out var uvs);
        m_Mesh.SetVertices(Array.ConvertAll(verts, v => (Vector3)v));
        m_Mesh.SetTriangles(tris, 0);
        m_Mesh.SetNormals(Array.ConvertAll(normals, v => (Vector3)v));
        m_Mesh.SetUVs(0, Array.ConvertAll(uvs, v => (Vector2)v));
        
        meshFilter.sharedMesh = m_Mesh;
    }

    private void OnDestroy()
    {
        if (m_GeneratedMesh && m_Mesh)
        {
            if (TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh == m_Mesh) meshFilter.sharedMesh = null;
            Destroy(m_Mesh);
        }
    }
}