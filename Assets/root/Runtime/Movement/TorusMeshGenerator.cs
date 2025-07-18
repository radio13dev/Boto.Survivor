using Unity.Mathematics;

public static class TorusMeshGenerator
{
    public enum Axis { x, y, z }

    /// <summary>
    /// Generates a torus mesh with customizable parameters.
    /// </summary>
    /// <param name="ringRadius">Distance from the center of the torus to the center of the tube.</param>
    /// <param name="thickness">Radius of the tube.</param>
    /// <param name="ringSegments">Number of segments around the main ring.</param>
    /// <param name="tubeSegments">Number of segments around the tube.</param>
    /// <param name="vertices">Output array of vertices.</param>
    /// <param name="triangles">Output array of triangle indices.</param>
    public static void GenerateTorusMesh(float ringRadius, float thickness, int ringSegments, int tubeSegments, Axis upAxis, out float3[] vertices, out int[] triangles, out float3[] normals)
    {
        // Total vertices count.
        vertices = new float3[ringSegments * tubeSegments];
        normals = new float3[vertices.Length];
        // Each quad on the torus yields two triangles -> 6 indices per quad.
        triangles = new int[ringSegments * tubeSegments * 6];

        // Compute vertices.
        for (int i = 0; i < ringSegments; i++)
        {
            float theta = 2 * math.PI * i / ringSegments;
            float cosTheta = math.cos(theta);
            float sinTheta = math.sin(theta);

            for (int j = 0; j < tubeSegments; j++)
            {
                float phi = 2 * math.PI * j / tubeSegments;
                float cosPhi = math.cos(phi);
                float sinPhi = math.sin(phi);

                // Parametric equation of a torus:
                // Position = (ringRadius + thickness * cos(phi)) * (cos(theta), sin(theta), 0) + (0, 0, thickness * sin(phi))
                float x = (ringRadius + thickness * cosPhi) * cosTheta;
                float y = (ringRadius + thickness * cosPhi) * sinTheta;
                float z = thickness * sinPhi;
                float3 point;
                
                if (upAxis == Axis.x)
                    point = new float3(z, y, x);
                else if (upAxis == Axis.y)
                    point = new float3(x, z, y);
                else
                    point = new float3(x, y, z);
                
                vertices[i * tubeSegments + j] = point;
                
                
                // Compute the torus circle center along the main ring.
                float3 circleCenter = new float3(ringRadius * cosTheta, ringRadius * sinTheta, 0f);
                // The normal is the direction from the circle center to the point.
                float3 normal = math.normalize(point - circleCenter);
                normals[i * tubeSegments + j] = normal;
            }
        }

        // Determine if triangle winding should be reversed.
        bool flipWinding = (upAxis == Axis.y);
        
        // Build triangle indices.
        int index = 0;
        for (int i = 0; i < ringSegments; i++)
        {
            // Wrap the ring index.
            int nextI = (i + 1) % ringSegments;

            for (int j = 0; j < tubeSegments; j++)
            {
                // Wrap the tube index.
                int nextJ = (j + 1) % tubeSegments;
                // Four vertices of the quad:
                int a = i * tubeSegments + j;
                int b = nextI * tubeSegments + j;
                int c = nextI * tubeSegments + nextJ;
                int d = i * tubeSegments + nextJ;

                if (flipWinding)
                {
                    // Flipped winding order.
                    triangles[index++] = a;
                    triangles[index++] = c;
                    triangles[index++] = b;

                    triangles[index++] = a;
                    triangles[index++] = d;
                    triangles[index++] = c;
                }
                else
                {
                    // Original winding order.
                    triangles[index++] = a;
                    triangles[index++] = b;
                    triangles[index++] = c;

                    triangles[index++] = a;
                    triangles[index++] = c;
                    triangles[index++] = d;
                }
            }
        }
    }
}