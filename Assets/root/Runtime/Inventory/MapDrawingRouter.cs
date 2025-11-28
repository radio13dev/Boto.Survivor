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

    public void ResetExistingMapPoints()
    {
        _mapDrawing.Clear();
        
        using var q = Game.ClientGame.World.EntityManager.CreateEntityQuery(typeof(PlayerControlled), typeof(MapDrawingData));
        q.SetSharedComponentFilter(new PlayerControlled(){ Index = (byte)PlayerIndexToCheck });
        if (!q.HasSingleton<MapDrawingData>()) { return; }
        
        var points = q.GetSingletonBuffer<MapDrawingData>();
        for (int i = 0; i < points.Length; i++)
        {
            _mapDrawing.AddPoint(points[i].DrawPoint, false);
        }
    }

    private void OnPlayerDrawMapPoint(Entity entity, int playerIndex, float3 pointPos)
    {
        if (!_mapDrawing || playerIndex != PlayerIndexToCheck || playerIndex == CameraTarget.MainTarget.PlayerIndex) { return; } 
        _mapDrawing.AddPoint(pointPos, false);
    }
}
