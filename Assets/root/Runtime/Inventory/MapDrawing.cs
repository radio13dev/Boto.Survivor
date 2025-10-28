using System;
using System.Collections.Generic;
using Drawing;
using Unity.Mathematics;
using UnityEngine;

public class MapDrawing : MonoBehaviour
{
    public List<Vector3> Path = new();
    public List<float2> PointsToroidal = new();
    
    public int SEGMENT_SIZE = 10;
    
    // Game settings
    public float Game_FloorOffset = 0.5f;
    public float Game_LineWidth = 2;
    public float Game_DashSize = 1;
    public float Game_GapSize = 1;
    public Color Game_LineColor = Color.white;
    public float Game_CircleRadius = 4;
    public Color Game_CircleColor = Color.white;
    public bool Game_ConnectLines = true;
    
    // Map settings
    public float Map_FloorOffset = 0.5f;
    public float Map_LineWidth = 2;
    public float Map_DashSize = 1;
    public float Map_GapSize = 1;
    public Color Map_LineColor = Color.white;
    public float Map_CircleRadius = 4;
    public Color Map_CircleColor = Color.white;
    public bool Map_ConnectLines = true;

    RedrawScope m_GameRedrawScope;
    RedrawScope m_MapRedrawScope;
    int m_Hash;

    private void Awake()
    {
        DrawingManager.allowRenderToRenderTextures = true;
        Redraw();
    }

    private void Update()
    {
        Redraw();
    }

    public void AddPoint(float3 point)
    {
        m_Hash++;
        PointsToroidal.Add(TorusMapper.CartesianToToroidal(point));
        if (PointsToroidal.Count > 1)
        {
            var path = new TorusMapper.Path(PointsToroidal[^2], PointsToroidal[^1]);
            for (int i = 0; i < SEGMENT_SIZE; i++)
            {
                var t = (float)i / (SEGMENT_SIZE - 1);
                var cartesianPoint = path.Evaluate(t);
                Path.Add(cartesianPoint);
            }
        }
        
        Redraw();
    }

    private void Redraw()
    {
        // Update the redraw scopes
        //m_GameRedrawScope.Dispose();
        //m_GameRedrawScope = DrawingManager.GetRedrawScope();
        //{
        //    var draw = DrawingManager.GetBuilder(m_GameRedrawScope, true);
        //    draw.cameraTargets = new Camera[]{ CameraRegistry.Main };
        //    using (draw.WithLineWidth(Game_LineWidth, Game_ConnectLines))
        //    {
        //        draw.DashedPolyline(Path, Game_DashSize, Game_GapSize, Game_LineColor);
        //        for (int i = 0; i < Path.Count; i+=SEGMENT_SIZE)
        //        {
        //            draw.SolidCircle(Path[i], TorusMapper.GetNormal(Path[i]), Game_CircleRadius, Game_CircleColor);
        //        }
        //        if (Path.Count > 0) // + Manually draw last point.
        //        {
        //            draw.SolidCircle(Path[^1], TorusMapper.GetNormal(Path[^1]), Game_CircleRadius, Game_CircleColor);
        //        }
        //    }
        //    draw.Dispose();
        //}
        //m_MapRedrawScope.Dispose();
        //m_MapRedrawScope = DrawingManager.GetRedrawScope();
        //{
        //    var draw = DrawingManager.GetBuilder(m_MapRedrawScope, true);
        //    draw.cameraTargets = new Camera[]{ CameraRegistry.Map };
        //    using (draw.WithLineWidth(Map_LineWidth, Map_ConnectLines))
        //    {
        //        draw.DashedPolyline(Path, Map_DashSize, Map_GapSize, Map_LineColor);
        //        for (int i = 0; i < Path.Count; i+=SEGMENT_SIZE)
        //        {
        //            draw.SolidCircle(Path[i], TorusMapper.GetNormal(Path[i]), Map_CircleRadius, Map_CircleColor);
        //        }
        //        if (Path.Count > 0) // + Manually draw last point.
        //        {
        //            draw.SolidCircle(Path[^1], TorusMapper.GetNormal(Path[^1]), Map_CircleRadius, Map_CircleColor);
        //        }
        //    }
        //    draw.Dispose();
        //}
        
        var mapHasher = new DrawingData.Hasher();
        mapHasher.Add(m_Hash);
        mapHasher.Add(1);
        if (!DrawingManager.TryDrawHasher(mapHasher))
        {
            var draw = DrawingManager.GetBuilder(renderInGame: true, hasher: mapHasher);
            draw.cameraTargets = new Camera[]{ CameraRegistry.Map };
            using (draw.WithLineWidth(Map_LineWidth, Map_ConnectLines))
            {
                draw.CatmullRom(Path, Game_LineColor);
                draw.DashedPolyline(Path, Map_DashSize, Map_GapSize, Map_LineColor);
                for (int i = 0; i < Path.Count; i+=SEGMENT_SIZE)
                {
                    draw.SolidCircle(Path[i], TorusMapper.GetNormal(Path[i]), Map_CircleRadius, Map_CircleColor);
                }
                if (Path.Count > 0) // + Manually draw last point.
                {
                    draw.SolidCircle(Path[^1], TorusMapper.GetNormal(Path[^1]), Map_CircleRadius, Map_CircleColor);
                }
            }
            draw.Dispose();
        }
        
        
        var gameHasher = new DrawingData.Hasher();
        gameHasher.Add(m_Hash);
        gameHasher.Add(2);
        if (!DrawingManager.TryDrawHasher(gameHasher))
        {
            var draw = DrawingManager.GetBuilder(renderInGame: true, hasher: gameHasher);
            draw.cameraTargets = new Camera[]{ CameraRegistry.Main };
            using (draw.WithLineWidth(Game_LineWidth, Game_ConnectLines))
            {
                draw.CatmullRom(Path, Game_LineColor);
                draw.DashedPolyline(Path, Game_DashSize, Game_GapSize, Game_LineColor);
                for (int i = 0; i < Path.Count; i+=SEGMENT_SIZE)
                {
                    draw.SolidCircle(Path[i], TorusMapper.GetNormal(Path[i]), Game_CircleRadius, Game_CircleColor);
                }
                if (Path.Count > 0) // + Manually draw last point.
                {
                    draw.SolidCircle(Path[^1], TorusMapper.GetNormal(Path[^1]), Game_CircleRadius, Game_CircleColor);
                }
            }
            draw.Dispose();
        }
    }

    private void OnDestroy()
    {
        m_GameRedrawScope.Dispose();
        m_MapRedrawScope.Dispose();
    }

    private void OnValidate()
    {
        m_Hash++;
    }
}