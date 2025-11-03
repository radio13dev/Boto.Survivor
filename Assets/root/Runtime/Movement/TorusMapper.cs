using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public static class TorusMapper
{
    public static readonly SharedStatic<float> RingRadius = SharedStatic<float>.GetOrCreate<RingRadiusKey>();

    private class RingRadiusKey
    {
    }

    public static readonly SharedStatic<float> Thickness = SharedStatic<float>.GetOrCreate<ThicknessKey>();

    private class ThicknessKey
    {
    }

    public static readonly SharedStatic<float> XRotScale = SharedStatic<float>.GetOrCreate<XRotScaleKey>();

    private class XRotScaleKey
    {
    }

    public static readonly SharedStatic<float> YRotScale = SharedStatic<float>.GetOrCreate<YRotScaleKey>();

    private class YRotScaleKey
    {
    }

    public static (float2 Min, float2 Max) MapBounds
    {
        get
        {
            var min = new float2(-math.PI / TorusMapper.XRotScale.Data, -math.PI / TorusMapper.YRotScale.Data);
            var max = new float2(math.PI / TorusMapper.XRotScale.Data, math.PI / TorusMapper.YRotScale.Data);

            return (min, max);
        }
    }

    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        RingRadius.Data = 160f;
        Thickness.Data = 80f;
        XRotScale.Data = 0.005f;
        YRotScale.Data = 0.013f;
    }
    
    static TorusMapper()
    {
        RingRadius.Data = 160f;
        Thickness.Data = 80f;
        XRotScale.Data = 0.005f;
        YRotScale.Data = 0.013f;
    }

    /// <summary>
    /// Converts a 2D position into a 3D point on a torus surface.
    /// localPos.x is used as theta and localPos.y as phi (radians).
    /// </summary>
    /// <param name="localPos">The 2D position to convert.</param>
    /// <returns>A 3D point on the torus.</returns>
    //[BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ConvertLocalPositionToTorus(float2 localPos)
    {
        float theta = localPos.x;
        float phi = localPos.y;

        var phiCos = math.cos(phi);

        float x = (RingRadius.Data + Thickness.Data * phiCos) * math.cos(theta);
        float y = (RingRadius.Data + Thickness.Data * phiCos) * math.sin(theta);
        float z = Thickness.Data * math.sin(phi);

        return new float3(x, y, z);
    }

    /// <summary>
    /// Converts a 2D local position into a 3D rotation where the object's up alignment is equal to the torus surface normal.
    /// The x coordinate is used as theta and the y coordinate as phi (in radians).
    /// </summary>
    /// <param name="localPos">2D position with angles in radians</param>
    /// <returns>A quaternion representing the rotation at that torus point</returns>
    //[BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetPositionAndUpRotation(float2 localPos, ref float3 Position, ref quaternion Rotation, ref float3 Normal)
    {
        float theta = localPos.x * XRotScale.Data;
        float phi = localPos.y * YRotScale.Data;
        float phiCos = math.cos(phi);
        float thetaCos = math.cos(theta);
        float thetaSin = math.sin(theta);

        float3 point = new float3(
            (RingRadius.Data + Thickness.Data * phiCos) * thetaCos,
            (RingRadius.Data + Thickness.Data * phiCos) * thetaSin,
            Thickness.Data * math.sin(phi)
        );

        // Compute the torus circle center along the main ring.
        float3 circleCenter = new float3(RingRadius.Data * thetaCos, RingRadius.Data * thetaSin, 0f);
        // The normal is the direction from the circle center to the point.
        float3 normal = math.normalize(point - circleCenter);

        // The tangent direction along the torus ring (derivative of [cos(theta), sin(theta)]).
        float3 tangent = new float3(-thetaSin, thetaCos, 0f);

        // Return a quaternion with forward as tangent and up as the torus surface normal.
        Position = point;
        Rotation = quaternion.LookRotation(tangent, normal);
        Normal = normal;
    }


    //[BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetTorusInfo(float3 worldSpace, out float3 circleCenter, out float3 surfaceNormal, out float3 surfaceTangent)
    {
        // Get torus parameters from SharedStatic
        float ringRadius = RingRadius.Data;

        // Compute the angle theta (in the X-z plane) for the projection
        float theta = math.atan2(worldSpace.z, worldSpace.x);

        // Compute the center of the main torus ring at this angle
        circleCenter = new float3(ringRadius * math.cos(theta), 0f, ringRadius * math.sin(theta));

        // Compute the offset from the ring center to worldSpace
        float3 offset = worldSpace - circleCenter;
        surfaceNormal = math.normalizesafe(offset);
        
        // Compute the tangent too. This value will smoothly rotate as you travel in any direction.
        // The tangent direction along the torus ring (derivative of [cos(theta), sin(theta)]).
        surfaceTangent = new float3(-math.sin(theta), 0f, math.cos(theta));
        
    }

    //[BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 GetTorusCircleCenter(float3 worldSpace)
    {
        // Get torus parameters from SharedStatic
        float ringRadius = RingRadius.Data;

        // Compute the angle theta (in the X-z plane) for the projection
        float theta = math.atan2(worldSpace.z, worldSpace.x);

        // Compute the center of the main torus ring at this angle
        return new float3(ringRadius * math.cos(theta), 0f, ringRadius * math.sin(theta));
    }

    //[BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClampAboveSurface(float3 worldSpace, float heightOffset, out float3 newPos, out bool clamped, out float3 normalIfClamped)
    {
        var circleCenter = GetTorusCircleCenter(worldSpace);

        // TODO: Do a check to reduce the lengthsq comps
        float3 offset = worldSpace - circleCenter;
        var lengthsq = math.lengthsq(offset);
        if (lengthsq >= math.square(Thickness.Data + heightOffset))
        {
            clamped = false;
            normalIfClamped = offset; // This isn't actually the normal
            newPos = worldSpace;
            return;
        }

        clamped = true;
        normalIfClamped = math.normalizesafe(offset);
        newPos = circleCenter + normalIfClamped * (Thickness.Data + heightOffset);
    }
    
    //[BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClampBelowSurface(float3 worldSpace, float heightOffset, out float3 newPos, out bool clamped, out float3 normalIfClamped)
    {
        var circleCenter = GetTorusCircleCenter(worldSpace);

        // TODO: Do a check to reduce the lengthsq comps
        float3 offset = worldSpace - circleCenter;
        var lengthsq = math.lengthsq(offset);
        if (lengthsq <= math.square(Thickness.Data - heightOffset))
        {
            clamped = false;
            normalIfClamped = -offset; // This isn't actually the normal
            newPos = worldSpace;
            return;
        }

        clamped = true;
        normalIfClamped = math.normalizesafe(-offset);
        newPos = circleCenter + normalIfClamped * (Thickness.Data - heightOffset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 SnapToSurface(float3 worldSpace)
    {
        SnapToSurface(worldSpace, 0, out var pos, out _);
        return pos;
    }
    
    //[BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SnapToSurface(float3 worldSpace, float heightOffset, out float3 newPos, out float3 normal)
    {
        var circleCenter = GetTorusCircleCenter(worldSpace);
        float3 offset = worldSpace - circleCenter;
        normal = math.normalizesafe(offset);
        newPos = circleCenter + normal * (Thickness.Data + heightOffset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 CartesianToToroidal(float3 position)
    {
        CartesianToToroidal(position, out float theta, out float phi, out _);
        return new float2(theta, phi);
    }

    //[BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CartesianToToroidal(float3 position, out float theta, out float phi, out float3 ringCenterOffset)
    {
        // Compute theta from the x-z plane.
        theta = math.atan2(position.z, position.x);

        // Obtain the ring center on the x-y plane.
        float3 ringCenter = new float3(
            TorusMapper.RingRadius.Data * math.cos(theta),
            0f,
            TorusMapper.RingRadius.Data * math.sin(theta)
        );
        // Derive the offset
        ringCenterOffset = position - ringCenter;
        
        // Determine the x-z plane distance from the ring center so we can determine phi
        var flatDistFromRing = math.length(ringCenterOffset.xz);
        if (math.lengthsq(position.xz) < math.square(TorusMapper.RingRadius.Data)) flatDistFromRing = -flatDistFromRing;
        
        // Calculate phi from the y component and distance from ring center.
        phi = math.atan2(position.y, flatDistFromRing);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ToroidalToCartesian(float2 toroidal, float height = 0)
    {
        return ToroidalToCartesian(toroidal.x, toroidal.y, height);
    }

    //[BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ToroidalToCartesian(float theta, float phi, float height = 0)
    {
        // Compute the cosine and sine for phi.
        float phiCos = math.cos(phi);
        // Calculate the x-z components based on ring radius and thickness.
        var thickness = TorusMapper.Thickness.Data + height;
        float x = (TorusMapper.RingRadius.Data + thickness * phiCos) * math.cos(theta);
        float y = thickness * math.sin(phi);
        float z = (TorusMapper.RingRadius.Data + thickness * phiCos) * math.sin(theta);

        return new float3(x, y, z);
    }

    public static float3 ProjectOntoSurface(float3 worldPos, float3 dir)
    {
        SnapToSurface(worldPos, 0, out _, out var normal);
        
        // Project the 'dir' vector onto the plane defined by the normal
        float3 projectedDir = dir - math.dot(dir, normal) * normal;
        return projectedDir;
    }
    
    public static float3 GetDirection(float3 a, float3 b)
    {
        var aT = CartesianToToroidal(a);
        var bT = CartesianToToroidal(b);
        var testT = aT + math.normalize(bT - aT)*0.01f;
        var test = ToroidalToCartesian(testT);
        return math.normalize(ProjectOntoSurface(a, test-a));
    }
    
    public static float3 GetNormal(float3 a)
    {
        SnapToSurface(a, 0, out _, out float3 normal);
        return normal;
    }
    
    public static quaternion GetNormalQuaternion(float3 a, float3 forward)
    {
        SnapToSurface(a, 0, out _, out float3 normal);
        return quaternion.LookRotationSafe(ProjectOntoSurface(a, forward) , a);
    }

    public static void CreateRectangularMesh(float3 a, float3 b, float width, float height, int segments, ref Mesh mesh)
    {
        // Iterate from a to b over 'segments' steps
        // For each step, create two vertices offset by 'width' along the vector perpendicular to the normal and the 'direction' of the a-b line
        var vertices = new Vector3[(segments + 1) * 3];
        CreateRectangularMesh(a,b,width, height, ref vertices);
        
        var uvs = new Vector2[(segments + 1) * 3];
        var indices = new int[segments * 12];
        for (int i = 0; i < segments; i++)
        {
            float t0 = (float)i / segments;
            uvs[i * 2] = new Vector2(t0, 0);
            uvs[i * 2 + 1] = new Vector2(t0, 0.5f);
            uvs[i * 2 + 2] = new Vector2(t0, 1);

            if (i < segments - 1)
            {
                int baseIndex = i * 12;
                int vertIndex = i * 3;
                indices[baseIndex] = vertIndex;
                indices[baseIndex + 1] = vertIndex + 1;
                indices[baseIndex + 2] = vertIndex + 3;

                indices[baseIndex + 3] = vertIndex + 1;
                indices[baseIndex + 4] = vertIndex + 4;
                indices[baseIndex + 5] = vertIndex + 3;
                
                indices[baseIndex + 6] = vertIndex + 1;
                indices[baseIndex + 7] = vertIndex + 2;
                indices[baseIndex + 8] = vertIndex + 4;
                
                indices[baseIndex + 9] = vertIndex + 2;
                indices[baseIndex + 10] = vertIndex + 5;
                indices[baseIndex + 11] = vertIndex + 4;
            }
        }
        
        // Create the mesh
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = indices;
        mesh.RecalculateNormals();
    }

    public static void CreateRectangularMesh(float3 a, float3 b, float width, float height, ref Vector3[] vertices)
    {
        var a0 = a;
        var majorDir = math.normalize(b - a);
        var len = math.distance(a, b);
        
        // Iterate from a to b over 'segments' steps
        // For each step, create a center vertex, and two shifted vertices offset by 'width'
        // along the vector perpendicular to the normal and the 'direction' of the a-b line
        var segments = vertices.Length / 3 - 1;
        for (int i = 0; i < segments; i++)
        {
            SnapToSurface(a,height, out a, out var up);
            majorDir = math.cross(up, math.cross(math.normalizesafe(majorDir), up));
            float3 perp = math.normalize(math.cross(up, majorDir)) * (width * 0.5f);

            vertices[i * 3] = a + perp - a0;
            vertices[i * 3 + 1] = a - a0;
            vertices[i * 3 + 2] = a - perp - a0;
            
            a += majorDir * len / segments;
        }
    }
    
    public static float3 MovePointInDirection(float3 a, float3 dir, int segments = 4)
    {
        var a0 = a;
        var majorDir = math.normalize(dir);
        var len = math.length(dir);
        var jump = len/segments;
        
        // Iterate from a to b over 'segments' steps
        // For each step, create a center vertex, and two shifted vertices offset by 'width'
        // along the vector perpendicular to the normal and the 'direction' of the a-b line
        for (int i = 0; i < segments; i++)
        {
            SnapToSurface(a, 0, out a, out var up);
            majorDir = math.cross(up, math.cross(math.normalizesafe(majorDir), up));
            a += majorDir * jump;
        }
        a = SnapToSurface(a);
        return a;
    }

    public static Path GetShortestPath(float3 a, float3 b)
    {
        var aT = CartesianToToroidal(a);
        var bT = CartesianToToroidal(b);
        
        return new Path(aT, bT);
    }
    
    public static float3 LerpCartesian(float3 a, float3 b, float t)
    {
        return GetShortestPath(a,b).Evaluate(t);
    }
    
    public struct Path
    {
        public readonly float2 AT;
        public readonly float2 BT;
        public Path(float2 aT, float2 bT)
        {
            AT = aT;
            BT = bT;
        }
        public void Write(ref NativeArray<float3> vertices)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                float t = (float)i / (vertices.Length - 1);
                float2 posT = math.float2(lerpangle(AT.x, BT.x, t), lerpangle(AT.y, BT.y, t));
                vertices[i] = ToroidalToCartesian(posT);
            }
        }
        public float3 Evaluate(float t)
        {
            float2 posT = math.float2(lerpangle(AT.x, BT.x, t), lerpangle(AT.y, BT.y, t));
            return ToroidalToCartesian(posT);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float lerpangle(float a, float b, float t)
        {
            var num = repeat(b - a, math.PI2);
            if (num > math.PI)
                num -= math.PI2;
            return a + num * math.clamp(t,0,1);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float bezierlerpangle(float a, float b, float c, float d, float t)
        {
            
            return lerpangle(lerpangle(a,b,t),lerpangle(c,d,t),t);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float repeat(float t, float length)
        {
            return math.clamp(t - math.floor(t / length) * length, 0.0f, length);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float deltaAngle(float a, float b)
        {
            var num = repeat(b - a, math.PI2);
            if (num > math.PI)
                num -= math.PI2;
            return num;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 deltaAngle(float2 a, float2 b)
        {
            return new float2(deltaAngle(a.x, b.x), deltaAngle(a.y, b.y));
        }
    }
}