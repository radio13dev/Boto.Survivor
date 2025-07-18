using System;
using Unity.Burst;
using UnityEngine;

public static class TorusMapper
{
    public static readonly SharedStatic<float> RingRadius = SharedStatic<float>.GetOrCreate<float, RingRadiusKey>();
    private class RingRadiusKey {}
    public static readonly SharedStatic<float> Thickness = SharedStatic<float>.GetOrCreate<float, ThicknessKey>();
    private class ThicknessKey {}
    public static readonly SharedStatic<float> XRotScale = SharedStatic<float>.GetOrCreate<float, XRotScaleKey>();
    private class XRotScaleKey {}
    public static readonly SharedStatic<float> YRotScale = SharedStatic<float>.GetOrCreate<float, YRotScaleKey>();
    private class YRotScaleKey {}
    
    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        RingRadius.Data = 100f;
        Thickness.Data = 5f;
        XRotScale.Data = 0.01f;
        YRotScale.Data = 0.1f;
    }
    
/*

/// <summary>
/// Converts a 2D position into a 3D point on a torus surface.
/// localPos.x is used as theta and localPos.y as phi (radians).
/// </summary>
/// <param name="localPos">The 2D position to convert.</param>
/// <returns>A 3D point on the torus.</returns>
[BurstCompile]
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static float3 ConvertLocalPositionToTorus(float2 localPos)
{
    float theta = localPos.x;
    float phi   = localPos.y;

    var phiCos = math.cos(phi);

    float x = (ringRadius + thickness * phiCos) * math.cos(theta);
    float y = (ringRadius + thickness * phiCos) * math.sin(theta);
    float z = thickness * math.sin(phi);

    return new float3(x, y, z);
}

/// <summary>
/// Converts a 2D local position into a 3D rotation where the object's up alignment is equal to the torus surface normal.
/// The x coordinate is used as theta and the y coordinate as phi (in radians).
/// </summary>
/// <param name="localPos">2D position with angles in radians</param>
/// <returns>A quaternion representing the rotation at that torus point</returns>
[BurstCompile]
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void GetPositionAndUpRotation(float2 localPos, ref float3 Position, ref quaternion Rotation, ref float3 Normal)
{
    float theta = localPos.x * xRotScale;
    float phi = localPos.y * yRotScale;
    float phiCos = math.cos(phi);
    float thetaCos = math.cos(theta);
    float thetaSin = math.sin(theta);

    float3 point = new float3(
        (ringRadius + thickness * phiCos) * thetaCos,
        (ringRadius + thickness * phiCos) * thetaSin,
        thickness * math.sin(phi)
    );

    // Compute the torus circle center along the main ring.
    float3 circleCenter = new float3(ringRadius * thetaCos, ringRadius * thetaSin, 0f);
    // The normal is the direction from the circle center to the point.
    float3 normal = math.normalize(point - circleCenter);

    // The tangent direction along the torus ring (derivative of [cos(theta), sin(theta)]).
    float3 tangent = new float3(-thetaSin, thetaCos, 0f);

    // Return a quaternion with forward as tangent and up as the torus surface normal.
    Position = point;
    Rotation = quaternion.LookRotation(tangent, normal);
    Normal = normal;
}
*/
}