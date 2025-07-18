using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct LocalTransform2D : IComponentData
{
    public float2 Position;
    public float Rotation;
    
    public float2 Forward => new float2(math.cos(Rotation), math.sin(Rotation));

    public static LocalTransform2D FromPosition(float2 rPos)
    {
        return new LocalTransform2D(){Position = rPos};
    }
}

public partial struct ConvertLocal2DToWorldSystem : ISystem
{
    const float xRotScale = 0.05f;
    const float yRotScale = 0.1f;
    // Constants from the torus definition.
    const float ringRadius = 20;
    const float thickness = 10;
    
    public void OnUpdate(ref SystemState state)
    {
        new Job().ScheduleParallel();
    }
    
    partial struct Job : IJobEntity
    {
        
        public void Execute(in LocalTransform2D local, ref LocalTransform world)
        {
            float3 position = default;
            quaternion rotation = default;
            float3 normal = default;

            {
                var localPos = local.Position;
                float theta = localPos.x * ConvertLocal2DToWorldSystem.xRotScale;
                float phi = localPos.y * ConvertLocal2DToWorldSystem.yRotScale;
                float phiCos = math.cos(phi);
                float thetaCos = math.cos(theta);
                float thetaSin = math.sin(theta);
        
                // Y-Pivot torus
                float3 point = new float3(
                    (ConvertLocal2DToWorldSystem.ringRadius + ConvertLocal2DToWorldSystem.thickness * phiCos) * thetaCos,
                    ConvertLocal2DToWorldSystem.thickness * math.sin(phi),
                    (ConvertLocal2DToWorldSystem.ringRadius + ConvertLocal2DToWorldSystem.thickness * phiCos) * thetaSin
                );
                
                /* Z-Pivot torus
                float3 point = new float3(
                    (ringRadius + thickness * phiCos) * thetaCos,
                    (ringRadius + thickness * phiCos) * thetaSin,
                    thickness * math.sin(phi)
                );
                */

                // Compute the torus circle center along the main ring.
                float3 circleCenter = new float3(ConvertLocal2DToWorldSystem.ringRadius * thetaCos, 0f, ConvertLocal2DToWorldSystem.ringRadius * thetaSin);
                // The normal is the direction from the circle center to the point.
                float3 pointNormal = math.normalize(point - circleCenter);

                // The tangent direction along the torus ring (derivative of [cos(theta), sin(theta)]).
                float3 tangent = new float3(-thetaSin, 0f, thetaCos);

                // Return a quaternion with forward as tangent and up as the torus surface normal.
                position = point;
                rotation = quaternion.LookRotation(-pointNormal, math.cross(tangent, pointNormal));//quaternion.LookRotation(tangent, pointNormal);
                normal = pointNormal;
            }
            
            //TorusMapper.GetPositionAndUpRotation(local.Position, ref position, ref rotation, ref normal);
            world = LocalTransform.FromPositionRotation(position, math.mul(quaternion.AxisAngle(normal, -local.Rotation), rotation));
        }
    }
}

/*
[BurstCompile]
public static class TorusMapper
{
    // Constants from the torus definition.
    const float ringRadius = 100f;
    const float thickness = 5f;
    
    public const float xRotScale = 0.01f;
    public const float yRotScale = 0.1f;
    
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
}
*/