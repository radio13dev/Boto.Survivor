using System.Runtime.CompilerServices;
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
        return new LocalTransform2D() { Position = rPos };
    }
}

public partial struct ConvertLocal2DToWorldSystem : ISystem
{
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
                float theta = localPos.x * TorusMapper.XRotScale.Data;
                float phi = localPos.y * TorusMapper.YRotScale.Data;
                float phiCos = math.cos(phi);
                float thetaCos = math.cos(theta);
                float thetaSin = math.sin(theta);

                // Y-Pivot torus
                float3 point = new float3(
                    (TorusMapper.RingRadius.Data + TorusMapper.Thickness.Data * phiCos) * thetaCos,
                    TorusMapper.Thickness.Data * math.sin(phi),
                    (TorusMapper.RingRadius.Data + TorusMapper.Thickness.Data * phiCos) * thetaSin
                );

                /* Z-Pivot torus
                float3 point = new float3(
                    (ringRadius + thickness * phiCos) * thetaCos,
                    (ringRadius + thickness * phiCos) * thetaSin,
                    thickness * math.sin(phi)
                );
                */

                // Compute the torus circle center along the main ring.
                float3 circleCenter = new float3(TorusMapper.RingRadius.Data * thetaCos, 0f, TorusMapper.RingRadius.Data * thetaSin);
                // The normal is the direction from the circle center to the point.
                float3 pointNormal = math.normalize(point - circleCenter);

                // The tangent direction along the torus ring (derivative of [cos(theta), sin(theta)]).
                float3 tangent = new float3(-thetaSin, 0f, thetaCos);

                // Return a quaternion with forward as tangent and up as the torus surface normal.
                position = point;
                rotation = quaternion.LookRotation(-pointNormal, math.cross(tangent, pointNormal)); //quaternion.LookRotation(tangent, pointNormal);
                normal = pointNormal;
            }

            //TorusMapper.GetPositionAndUpRotation(local.Position, ref position, ref rotation, ref normal);
            world = LocalTransform.FromPositionRotation(position, math.mul(quaternion.AxisAngle(normal, -local.Rotation), rotation));
        }
    }
}