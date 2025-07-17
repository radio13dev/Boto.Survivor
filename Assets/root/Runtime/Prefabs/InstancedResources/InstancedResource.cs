using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/InstancedResourcesDatabase", order = 1)]
public class InstancedResource : ScriptableObject
{
    public Material Material;
    public Mesh Mesh;
    public SpriteAnimData AnimData;
    
    public bool Animated => AnimData.Frames > 0;

    internal RenderParams RenderParams
    {
        get
        {
            if (!m_Setup)
            {
                m_Setup = true;
                m_RenderParams = new RenderParams(Material)
                {
                    matProps = new()
                };
                m_RenderParams.matProps.SetFloatArray("spriteAnimFrameBuffer", new float[Profiling.k_MaxInstances]);
            }
            
            return m_RenderParams;
        }
    }

    [NonSerialized] bool m_Setup;
    [NonSerialized] RenderParams m_RenderParams;
}