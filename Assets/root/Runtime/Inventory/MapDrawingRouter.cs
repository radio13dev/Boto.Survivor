using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class MapDrawingRouter : MonoBehaviour
{
    [SerializeField] private MapDrawing _mapDrawing;
    public int PlayerIndexToCheck;
    
    private void OnEnable()
    {
        GameEvents.OnPlayerDrawMapPoint += OnPlayerDrawMapPoint;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerDrawMapPoint -= OnPlayerDrawMapPoint;
    }

    private void OnPlayerDrawMapPoint(Entity entity, int playerIndex, float3 pointPos)
    {
        if (!_mapDrawing || playerIndex != PlayerIndexToCheck || playerIndex == CameraTarget.MainTarget.PlayerIndex) { return; } 
        _mapDrawing.AddPoint(pointPos, false);
    }
}
