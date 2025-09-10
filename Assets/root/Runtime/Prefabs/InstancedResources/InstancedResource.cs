using System;
using Unity.Entities;
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/InstancedResourcesDatabase", order = 1)]
public class InstancedResource : ScriptableObject
{
    public Material Material;
    public Mesh Mesh;
    public SpriteAnimData AnimData;
    public bool HasLifespan;
    public bool UseLastTransform;
    public bool IsTorus;
    public bool IsCone;
    
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
                if (Animated) m_RenderParams.matProps.SetFloatArray("spriteAnimFrameBuffer", new float[Profiling.k_MaxInstances]);
                if (HasLifespan) m_RenderParams.matProps.SetFloatArray("lifespanBuffer", new float[Profiling.k_MaxInstances]);
                if (IsTorus) m_RenderParams.matProps.SetFloatArray("torusMinBuffer", new float[Profiling.k_MaxInstances]);
                if (IsCone) m_RenderParams.matProps.SetFloatArray("torusAngleBuffer", new float[Profiling.k_MaxInstances]);
            }
            
            return m_RenderParams;
        }
    }

    [NonSerialized] [HideInInspector] internal EntityQuery Query;
    [NonSerialized] bool m_Setup;
    [NonSerialized] RenderParams m_RenderParams;
}