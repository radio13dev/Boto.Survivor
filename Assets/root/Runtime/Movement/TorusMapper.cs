using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

public static class TorusMapper
{
    public static readonly SharedStatic<float> RingRadius = SharedStatic<float>.GetOrCreate<float, RingRadiusKey>();

    private class RingRadiusKey
    {
    }

    public static readonly SharedStatic<float> Thickness = SharedStatic<float>.GetOrCreate<float, ThicknessKey>();

    private class ThicknessKey
    {
    }

    public static readonly SharedStatic<float> XRotScale = SharedStatic<float>.GetOrCreate<float, XRotScaleKey>();

    private class XRotScaleKey
    {
    }

    public static readonly SharedStatic<float> YRotScale = SharedStatic<float>.GetOrCreate<float, YRotScaleKey>();

    private class YRotScaleKey
    {
    }

    public static readonly SharedStatic<float> RingRadiusSq = SharedStatic<float>.GetOrCreate<float, RingRadiusSqKey>();

    private class RingRadiusSqKey
    {
    }

    public static readonly SharedStatic<float> ThicknessSq = SharedStatic<float>.GetOrCreate<float, ThicknessSqKey>();

    private class ThicknessSqKey
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
        RingRadius.Data = 80f;
        Thickness.Data = 40f;
        XRotScale.Data = 0.005f;
        YRotScale.Data = 0.013f;
        RingRadiusSq.Data = RingRadius.Data * RingRadius.Data;
        ThicknessSq.Data = Thickness.Data * Thickness.Data;
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
    public static void GetTorusInfo(float3 worldSpace, out float3 circleCenter, out float3 surfaceNormal)
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
    public static void ClampAboveSurface(float3 worldSpace, out float3 newPos, out bool clamped, out float3 normalIfClamped)
    {
        var circleCenter = GetTorusCircleCenter(worldSpace);

        // TODO: Do a check to reduce the lengthsq comps
        float3 offset = worldSpace - circleCenter;
        var lengthsq = math.lengthsq(offset);
        if (lengthsq >= ThicknessSq.Data)
        {
            clamped = false;
            normalIfClamped = offset; // This isn't actually the normal
            newPos = worldSpace;
            return;
        }

        clamped = true;
        normalIfClamped = math.normalizesafe(offset);
        newPos = circleCenter + normalIfClamped * Thickness.Data;
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
        // Calculate phi from the y component and distance from ring center.
        phi = math.atan2(position.y, math.length(ringCenterOffset));
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
}