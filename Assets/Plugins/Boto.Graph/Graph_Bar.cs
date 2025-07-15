using Unity.Mathematics;
using UnityEngine;

public class Graph_Bar : Graph
{
    protected override void AddColumnVertices(Point pos)
    {
        // Calculate the data point position.
        float x = pos.x * _genXStep + _genXZero;
        float y = pos.y * _genYStep + _genYZero;

        // Define bar width as 80% of the horizontal step.
        float barWidth = _genXStep * 1f;
        float halfBar = barWidth * 0.5f;

        // Use the current vertex index as the starting index for this bar.
        int p1 = _vertexIndex;
        UIVertex vert = _vert;

        // Bottom left (bar starts at baseline, assumed 0)
        vert.position = new float3(x - halfBar, _genYZero, 0);
        if (pos.color.HasValue)
            vert.color = pos.color.Value;
        _vertexStream.Add(vert);

        // Top left
        vert.position = new float3(x - halfBar, y, 0);
        _vertexStream.Add(vert);

        // Top right
        vert.position = new float3(x + halfBar, y, 0);
        _vertexStream.Add(vert);

        // Bottom right
        vert.position = new float3(x + halfBar, _genYZero, 0);
        _vertexStream.Add(vert);

        _vertexIndex += 4;

        // Create two triangles to form the rectangle.
        _triangleStream.Add(p1);     // bottom left
        _triangleStream.Add(p1 + 1); // top left
        _triangleStream.Add(p1 + 2); // top right

        _triangleStream.Add(p1);     // bottom left
        _triangleStream.Add(p1 + 2); // top right
        _triangleStream.Add(p1 + 3); // bottom right

        // (Optional) Update the last position and vertex, if your base class uses them.
        LastPosition = new float2(x, y);
        LastVertex = p1;
        PointIndex++;
    }
}