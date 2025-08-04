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
    public static void GenerateTorusMesh(float ringRadius, float thickness, int ringSegments, int tubeSegments, Axis upAxis, out float3[] vertices, out int[] triangles, out float3[] normals, out float2[] uvs)
    {
        // Total vertices count.
        vertices = new float3[ringSegments * tubeSegments];
        normals = new float3[vertices.Length];
        uvs = new float2[vertices.Length];
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
                float3 circleCenter;
                
                if (upAxis == Axis.x)
                {
                    point = new float3(z, y, x);
                    circleCenter = new float3(0, ringRadius*sinTheta, ringRadius*cosTheta);
                }
                else if (upAxis == Axis.y)
                {
                    point = new float3(x, z, y);
                    circleCenter = new float3(ringRadius*cosTheta, 0, ringRadius*sinTheta);
                }
                else
                {
                    point = new float3(x, y, z);
                    circleCenter = new float3(ringRadius*cosTheta, ringRadius*sinTheta, 0);
                }
                
                vertices[i * tubeSegments + j] = point;
                
                // The normal is the direction from the circle center to the point.
                float3 normal = math.normalize(point - circleCenter);
                normals[i * tubeSegments + j] = normal;
                
                uvs[i*tubeSegments + j] = new float2((float)i/(ringSegments-1), (float)j/(tubeSegments-1));
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


public static class QuadMeshGenerator
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
    public static void GenerateQuadMesh(float width, float height, Axis axis, out float3[] vertices, out int[] triangles, out float3[] normals, out float2[] uvs)
    {
        // Total vertices count.
        vertices = new float3[4];
        normals = new float3[vertices.Length];
        uvs = new float2[vertices.Length];
        // Each quad on the torus yields two triangles -> 6 indices per quad.
        triangles = new int[6];

        // Compute vertices.
        if (axis == Axis.x)
        {
            vertices[0] = new float3(0, width/2, height/2);
            vertices[1] = new float3(0, width/2, -height/2);
            vertices[2] = new float3(0, -width/2, -height/2);
            vertices[3] = new float3(0, -width/2, height/2);
            
            normals[0] = new float3(1, 0, 0);
            normals[1] = new float3(1, 0, 0);
            normals[2] = new float3(1, 0, 0);
            normals[3] = new float3(1, 0, 0);
        }
        else if (axis == Axis.y)
        {
            vertices[0] = new float3(width/2, 0, height/2);
            vertices[1] = new float3(width/2, 0, -height/2);
            vertices[2] = new float3(-width/2, 0, -height/2);
            vertices[3] = new float3(-width/2, 0, height/2);
            
            normals[0] = new float3(0, 1, 0);
            normals[1] = new float3(0, 1, 0);
            normals[2] = new float3(0, 1, 0);
            normals[3] = new float3(0, 1, 0);
        }
        else if (axis == Axis.y)
        {
            vertices[0] = new float3( width/2, height/2, 0);
            vertices[1] = new float3( width/2, -height/2, 0);
            vertices[2] = new float3( -width/2, -height/2, 0);
            vertices[3] = new float3( -width/2, height/2, 0);
            
            normals[0] = new float3(0, 0, 1);
            normals[1] = new float3(0, 0, 1);
            normals[2] = new float3(0, 0, 1);
            normals[3] = new float3(0, 0, 1);
        }
            
        uvs[0] = new float2(1, 1);
        uvs[1] = new float2(1, 0);
        uvs[2] = new float2(0, 0);
        uvs[3] = new float2(0, 1);
            
        triangles[0] = 0;
        triangles[1] = 1;
        triangles[2] = 3;
            
        triangles[3] = 1;
        triangles[4] = 2;
        triangles[5] = 3;
    }
}