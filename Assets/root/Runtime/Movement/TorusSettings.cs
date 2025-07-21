using UnityEngine;

public class TorusSettings : MonoBehaviour
{
    public float RingRadius = 100f;
    public float Thickness = 5f;
    public float XRotScale = 0.01f;
    public float YRotScale = 0.1f;
    
    public int RingSegments = 30;
    public int TubeSegments = 5;
    public float CharacterHeightOffset = 0.5f;

    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        
        TorusMapper.RingRadius.Data = RingRadius;
        TorusMapper.Thickness.Data = Thickness;
        TorusMapper.XRotScale.Data = XRotScale;
        TorusMapper.YRotScale.Data = YRotScale;
    }
    
    [EditorButton]
    private void RegenerateMesh()
    {
        if (TryGetComponent<TorusTerrainTool>(out var meshGen))
            meshGen.GenerateMesh(TorusMapper.RingRadius.Data, TorusMapper.Thickness.Data-CharacterHeightOffset, RingSegments, TubeSegments);
    }
}