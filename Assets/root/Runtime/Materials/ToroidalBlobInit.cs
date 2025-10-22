using System;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

[ExecuteInEditMode]
public class ToroidalBlobInit : MonoBehaviour
{
    public const int BLOB_COUNT = 3;
    public const int METABALL_COUNT = 20;

    public Blob[] Blobs = new Blob[BLOB_COUNT];
    static bool m_BlobsDirty;
    
    public Material ZeroMat;
    
    [Serializable]
    public struct Blob
    {
        public Color A;
        public Color B;
        public Color Border;
    }

    private void Start()
    {
        ApplyToShader();
    }

    private void OnValidate()
    {
        if (Blobs.Length != BLOB_COUNT) Array.Resize(ref Blobs, BLOB_COUNT);
        ApplyToShader();
    }
    
    public void ApplyToShader()
    {
        var cA = ZeroMat.GetColor("_HashAColor");
        var cB = ZeroMat.GetColor("_HashBColor");

        Shader.SetGlobalVectorArray("_blob_acolor", Blobs.Select(c => RGBToHSV(c.A) - RGBToHSV(cA)).ToArray());
        Shader.SetGlobalVectorArray("_blob_bcolor", Blobs.Select(c => RGBToHSV(c.B) - RGBToHSV(cB)).ToArray());
        Shader.SetGlobalVectorArray("_blob_border", Blobs.Select(c => RGBToHSV(c.Border) - RGBToHSV(cA)).ToArray());
        
        int index = 0;
        ToroidalBlobMono[] metaballs = new ToroidalBlobMono[METABALL_COUNT];
        foreach (var metaball in Object.FindObjectsByType<ToroidalBlobMono>(FindObjectsSortMode.None))
        {
            if (index >= metaballs.Length) break;
            
            metaballs[index] = metaball;
            index++;
        }
        Shader.SetGlobalVectorArray("_metaball_position", metaballs.Select(b => b ? (Vector4)b.transform.position : Vector4.zero).ToArray());
        Shader.SetGlobalFloatArray("_metaball_radiussqr", metaballs.Select(b => b ? b.Radius*b.Radius : 0).ToArray());
        Shader.SetGlobalFloatArray("_metaball_index", metaballs.Select(b => b ? (float)b.Index : 0).ToArray());
    }
    
    static Vector4 RGBToHSV(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        return new Vector4(h,s,v,c.a);
    }

    private void Update()
    {
        if (m_BlobsDirty)
        {
            m_BlobsDirty = false;
            ApplyToShader();
        }
    }

    public static void SetDirty()
    {
        m_BlobsDirty = true;
    }
}
