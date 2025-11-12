using Unity.Mathematics;

public static class TorusMeshGenerator
{
    public enum Axis
    {
        x,
        y,
        z
    }

    /// <summary>
    /// Generates a torus mesh with customizable parameters.
    /// </summary>
    /// <param name="ringRadius">Distance from the center of the torus to the center of the tube.</param>
    /// <param name="thickness">Radius of the tube.</param>
    /// <param name="ringSegments">Number of segments around the main ring.</param>
    /// <param name="tubeSegments">Number of segments around the tube.</param>
    /// <param name="vertices">Output array of vertices.</param>
    /// <param name="triangles">Output array of triangle indices.</param>
    public static void GenerateTorusMesh(
        float ringRadius, float thickness, int ringSegments, int tubeSegments, Axis upAxis,
        out float3[] vertices, out int[] triangles, out float3[] normals, out float2[] uvs, float sectionAngleRadians = math.PI2)
    {
        int vertCountR = ringSegments + 1;
        int vertCountT = tubeSegments + 1;

        vertices = new float3[vertCountR * vertCountT];
        normals = new float3[vertices.Length];
        uvs = new float2[vertices.Length];
        triangles = new int[ringSegments * tubeSegments * 6];

        for (int i = 0; i < vertCountR; i++)
        {
            float theta = sectionAngleRadians * i / ringSegments;
            float cosTheta = math.cos(theta);
            float sinTheta = math.sin(theta);

            for (int j = 0; j < vertCountT; j++)
            {
                float phi = 2 * math.PI * j / tubeSegments;
                float cosPhi = math.cos(phi);
                float sinPhi = math.sin(phi);

                float x = (ringRadius + thickness * cosPhi) * cosTheta;
                float y = (ringRadius + thickness * cosPhi) * sinTheta;
                float z = thickness * sinPhi;
                float3 point;
                float3 circleCenter;

                if (upAxis == Axis.x)
                {
                    point = new float3(z, y, x);
                    circleCenter = new float3(0, ringRadius * sinTheta, ringRadius * cosTheta);
                }
                else if (upAxis == Axis.y)
                {
                    point = new float3(x, z, y);
                    circleCenter = new float3(ringRadius * cosTheta, 0, ringRadius * sinTheta);
                }
                else
                {
                    point = new float3(x, y, z);
                    circleCenter = new float3(ringRadius * cosTheta, ringRadius * sinTheta, 0);
                }

                int vertIndex = i * vertCountT + j;
                vertices[vertIndex] = point;
                normals[vertIndex] = math.normalize(point - circleCenter);
                uvs[vertIndex] = new float2((float)i / ringSegments, (float)j / tubeSegments);
            }
        }

        bool flipWinding = (upAxis == Axis.y);
        int index = 0;

        for (int i = 0; i < ringSegments; i++)
        {
            for (int j = 0; j < tubeSegments; j++)
            {
                int a = i * vertCountT + j;
                int b = (i + 1) * vertCountT + j;
                int c = (i + 1) * vertCountT + (j + 1);
                int d = i * vertCountT + (j + 1);

                if (flipWinding)
                {
                    triangles[index++] = a;
                    triangles[index++] = c;
                    triangles[index++] = b;

                    triangles[index++] = a;
                    triangles[index++] = d;
                    triangles[index++] = c;
                }
                else
                {
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


    /// <summary>
    /// Generates a torus mesh with customizable parameters.
    /// </summary>
    /// <param name="ringRadius">Distance from the center of the torus to the center of the tube.</param>
    /// <param name="thickness">Radius of the tube.</param>
    /// <param name="ringSegments">Number of segments around the main ring.</param>
    /// <param name="tubeSegments">Number of segments around the tube.</param>
    /// <param name="vertices">Output array of vertices.</param>
    /// <param name="triangles">Output array of triangle indices.</param>
    public static void GenerateTorusBlob(
        uint seed,
        float3 zero, float groundOffset, int blobSegments, float blobSize, int triangleSegments,
        out float3[] vertices, out int[] triangles, out float3[] normals, out float2[] uvs)
    {
        Random r = Random.CreateFromIndex(seed);

        vertices = new float3[blobSegments + 1];
        normals = new float3[vertices.Length];
        uvs = new float2[vertices.Length];
        triangles = new int[(blobSegments - 1) * 3];

        TorusMapper.SnapToSurface(zero, 0, out zero, out float3 zeroNormal);

        // Get the forward and right vectors from the zeroNormal
        float3 forward = math.cross(zeroNormal, math.right());
        float3 right = math.cross(zeroNormal, forward);

        // Create a 'blob' shaped polygon around the zero point on the x-z plane.
        for (int i = 0; i < blobSegments; i++)
        {
            float ang = math.PI2 * i / blobSegments;
            float3 dir = math.cos(ang) * forward + math.sin(ang) * right;
            float3 target = dir * blobSize;

            // Shift the target in a random direction
            var shiftScale = 0.4f * math.PI2 * blobSize / blobSegments;
            var shift = r.NextFloat2Direction() * shiftScale;
            target += shift.x * forward + shift.y * right;

            var len = math.length(target);
            dir = math.normalize(target);

            var point = zero;
            for (int j = 0; j < triangleSegments; j++)
            {
                point = TorusMapper.MovePointInDirection(point, dir * len / triangleSegments, 1);
            }
        }

        // Close the loop
        triangles[^3] = 0;
        triangles[^2] = 1;
        triangles[^1] = vertices.Length - 1;
    }
    
        /// <summary>
    /// Generates a torus mesh with customizable parameters.
    /// </summary>
    /// <param name="ringRadius">Distance from the center of the torus to the center of the tube.</param>
    /// <param name="thickness">Radius of the tube.</param>
    /// <param name="ringSegments">Number of segments around the main ring.</param>
    /// <param name="tubeSegments">Number of segments around the tube.</param>
    /// <param name="vertices">Output array of vertices.</param>
    /// <param name="triangles">Output array of triangle indices.</param>
    public static void GenerateCylinderMesh(
    float thickness, int ringSegments, int tubeSegments, Axis upAxis,
    out float3[] vertices, out int[] triangles, out float3[] normals, out float2[] uvs)
{
    int vertCountR = ringSegments + 1;
    int vertCountT = tubeSegments + 1;

    vertices = new float3[vertCountR * vertCountT];
    normals = new float3[vertices.Length];
    uvs = new float2[vertices.Length];
    triangles = new int[ringSegments * tubeSegments * 6];

    for (int i = 0; i < vertCountR; i++)
    {
        float theta = math.PI2 * i / ringSegments;
        float cosTheta = math.cos(theta);
        float sinTheta = math.sin(theta);

        for (int j = 0; j < vertCountT; j++)
        {
            float t = (float)j / tubeSegments;

            float x = thickness * cosTheta;
            float y = thickness * sinTheta;
            float z = thickness * t;
            float3 point;
            float3 circleCenter;

            if (upAxis == Axis.x)
            {
                point = new float3(z, y, x);
                circleCenter = new float3(z, 0, 0);
            }
            else if (upAxis == Axis.y)
            {
                point = new float3(x, z, y);
                circleCenter = new float3(0, z, 0);
            }
            else
            {
                point = new float3(x, y, z);
                circleCenter = new float3(0, 0, z);
            }

            int vertIndex = i * vertCountT + j;
            vertices[vertIndex] = point;
            normals[vertIndex] = math.normalize(point - circleCenter);
            uvs[vertIndex] = new float2((float)i / ringSegments, (float)j / tubeSegments);
        }
    }

    bool flipWinding = (upAxis == Axis.y);
    int index = 0;

    for (int i = 0; i < ringSegments; i++)
    {
        for (int j = 0; j < tubeSegments; j++)
        {
            int a = i * vertCountT + j;
            int b = (i + 1) * vertCountT + j;
            int c = (i + 1) * vertCountT + (j + 1);
            int d = i * vertCountT + (j + 1);

            if (flipWinding)
            {
                triangles[index++] = a;
                triangles[index++] = c;
                triangles[index++] = b;

                triangles[index++] = a;
                triangles[index++] = d;
                triangles[index++] = c;
            }
            else
            {
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
    public enum Axis
    {
        x,
        y,
        z
    }

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
            vertices[0] = new float3(0, width / 2, height / 2);
            vertices[1] = new float3(0, width / 2, -height / 2);
            vertices[2] = new float3(0, -width / 2, -height / 2);
            vertices[3] = new float3(0, -width / 2, height / 2);

            normals[0] = new float3(1, 0, 0);
            normals[1] = new float3(1, 0, 0);
            normals[2] = new float3(1, 0, 0);
            normals[3] = new float3(1, 0, 0);
        }
        else if (axis == Axis.y)
        {
            vertices[0] = new float3(width / 2, 0, height / 2);
            vertices[1] = new float3(width / 2, 0, -height / 2);
            vertices[2] = new float3(-width / 2, 0, -height / 2);
            vertices[3] = new float3(-width / 2, 0, height / 2);

            normals[0] = new float3(0, 1, 0);
            normals[1] = new float3(0, 1, 0);
            normals[2] = new float3(0, 1, 0);
            normals[3] = new float3(0, 1, 0);
        }
        else if (axis == Axis.y)
        {
            vertices[0] = new float3(width / 2, height / 2, 0);
            vertices[1] = new float3(width / 2, -height / 2, 0);
            vertices[2] = new float3(-width / 2, -height / 2, 0);
            vertices[3] = new float3(-width / 2, height / 2, 0);

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