using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

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
    public bool IsColorBaseColor;
    public bool IsColorA;
    public bool IsColorB;
    public bool IsColorC;
    public bool ShowOnMap;
    
    public bool IsStretch;

    public bool Animated => AnimData.Frames > 0;

    internal RenderParams RenderParams
    {
        get
        {
            if (!m_Setup)
                Setup();

            return m_RenderParams;
        }
    }

    private void Setup()
    {
        m_Setup = true;
        m_RenderParams = new RenderParams(Material)
        {
            matProps = new()
        };
        SetupProps(m_RenderParams.matProps);
        
        if (ShowOnMap)
        {
            // These render params should be on the 'Map' layer
            m_MapRenderParams = new RenderParams(Material)
            {
                matProps = new()
            };
            SetupProps(m_MapRenderParams.matProps);
            m_MapRenderParams.matProps.SetFloat("_IsMap", 1);
            m_MapRenderParams.layer = LayerMask.NameToLayer("Map");
            
        }
        
        void SetupProps(in MaterialPropertyBlock matProps)
        {
            if (Animated) matProps.SetFloatArray("spriteAnimFrameBuffer", new float[Profiling.k_MaxInstances]);
            if (HasLifespan) matProps.SetFloatArray("lifespanBuffer", new float[Profiling.k_MaxInstances]);
            if (IsTorus) matProps.SetFloatArray("torusMinBuffer", new float[Profiling.k_MaxInstances]);
            if (IsCone) matProps.SetFloatArray("torusAngleBuffer", new float[Profiling.k_MaxInstances]);
            if (IsStretch) matProps.SetVectorArray("stretchBuffer", new Vector4[Profiling.k_MaxInstances]);
        
            if (IsColorBaseColor) matProps.SetVectorArray("colorBaseColorBuffer", new Vector4[Profiling.k_MaxInstances]);
            if (IsColorA) matProps.SetVectorArray("colorABuffer", new Vector4[Profiling.k_MaxInstances]);
            if (IsColorB) matProps.SetVectorArray("colorBBuffer", new Vector4[Profiling.k_MaxInstances]);
            if (IsColorC) matProps.SetVectorArray("colorCBuffer", new Vector4[Profiling.k_MaxInstances]);
        }
    }

    internal RenderParams MapRenderParams
    {
        get
        {
            if (!m_Setup)
                Setup();
            return m_MapRenderParams;
        }
    }

    [NonSerialized] bool m_Setup;
    [NonSerialized] RenderParams m_RenderParams;
    [NonSerialized] RenderParams m_MapRenderParams;
}