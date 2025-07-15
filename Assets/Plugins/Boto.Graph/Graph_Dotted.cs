using Unity.Mathematics;
using UnityEngine;

public class Graph_Dotted : Graph
{
    protected override void AddColumnVertices(Point pos)
    {
        float x = pos.x * _genXStep + _genXZero;
        float y = pos.y * _genYStep + _genYZero;

        float2 newPosition = new float2(x, y);
        if (LastPosition == null)
            LastPosition = newPosition + new float2(-1, 0);
            
        // Just draw a circle here.
        int CIRCLE_POINTS = 5;
        float CIRCLE_ANGLE = Mathf.PI*2.0f/5.0f;
        
        int p1 = _vertexIndex;
        var capStart = new float2(1,0);
        for (int i = 1; i <= CIRCLE_POINTS; i++)
        {
            var capDir = VectorUtilities.RotateVector2D(capStart, -i * CIRCLE_ANGLE);//negative for clockwise
            var vert = _vert;
            vert.position = (newPosition + capDir * lineHeight).xy0();
            if (pos.color.HasValue) vert.color = pos.color.Value;
            _vertexStream.Add(vert);
            _vertexIndex++;
        }

        //Link the triangles
        int vertexIterator = 0;
        for (; vertexIterator < CIRCLE_POINTS - 2; vertexIterator++)
        {
            _triangleStream.Add(p1);
            _triangleStream.Add(p1 + vertexIterator + 1);
            _triangleStream.Add(p1 + vertexIterator + 2);
        }
            
        LastPosition = newPosition;
        LastVertex = p1;
        PointIndex++;
    }
}