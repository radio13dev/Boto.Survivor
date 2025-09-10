using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BovineLabs.Core.Utility;
using BovineLabs.Saving;
using Drawing;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using AABB = NativeTrees.AABB;

namespace Collisions
{
    /// <summary>
    /// Component only used for rendering
    /// </summary>
    public struct TorusMin : IComponentData
    {
        public float Value;

        public TorusMin(float value)
        {
            Value = value;
        }
    }
    /// <summary>
    /// Component only used for rendering
    /// </summary>
    public struct TorusCone : IComponentData
    {
        public float Value;

        public TorusCone(float value)
        {
            Value = value;
        }
    }

    /// <summary>
    /// Projectile collision is predicted, only after all movement is done
    /// </summary>
    [UpdateAfter(typeof(MovementSystemGroup))]
    [UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
    public partial class CollisionSystemGroup : ComponentSystemGroup
    {
    }

    public enum ColliderType
    {
        AABB,
        Sphere,
        Torus,
        TorusCone,
    }

    public struct ColliderData : IBufferElementData
    {
        static SharedStatic<NativeArray<ColliderData>> s_Data;

        public NativeTrees.AABB Value;
        public float TorusMin;
    }

    [Save]
    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = 64, Pack = 32)]
    public readonly struct Collider : IComponentData, IEnableableComponent
    {
        [SerializeField] [FieldOffset(0)] public readonly AABB AABB;
        [SerializeField] [FieldOffset(24)] public readonly ColliderType Type;

        [SerializeField] [FieldOffset(28)] public readonly float _A;
        [SerializeField] [FieldOffset(32)] public readonly float _B;
        [SerializeField] [FieldOffset(36)] public readonly float _C;
        [SerializeField] [FieldOffset(40)] public readonly float _D;
        [SerializeField] [FieldOffset(44)] public readonly float _E;
        [SerializeField] [FieldOffset(48)] public readonly float _F;

        [FieldOffset(28)] public readonly float RadiusSqr;
        [FieldOffset(32)] public readonly float TorusMinSqr;
        [FieldOffset(36)] public readonly float ConeAngle;
        [FieldOffset(40)] public readonly float3 ConeDir;

        [Pure] public float3 Center => AABB.Center;
        [Pure] public float Radius => AABB.Size.x / 2;
        [Pure] public float TorusMin => math.sqrt(TorusMinSqr);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public NativeTrees.AABB Add(LocalTransform t)
        {
            return new NativeTrees.AABB(AABB.min * t.Scale + t.Position, AABB.max * t.Scale + t.Position);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public Collider Apply(LocalTransform transform)
        {
            var aabb = Add(transform);
            switch (Type)
            {
                case ColliderType.AABB:
                    return new Collider(aabb, ColliderType.AABB);
                case ColliderType.Sphere:
                    return new Collider(aabb, ColliderType.Sphere, math.square(aabb.Size.x/2));
                case ColliderType.Torus:
                    return new Collider(aabb, ColliderType.Torus, math.square(aabb.Size.x/2), TorusMinSqr * transform.Scale * transform.Scale);
                case ColliderType.TorusCone:
                    return new Collider(aabb, ColliderType.TorusCone, math.square(aabb.Size.x/2), TorusMinSqr * transform.Scale * transform.Scale, ConeAngle,
                        math.mul(transform.Rotation, ConeDir));
                default:
                    throw new NotImplementedException("Unimplemented collider type: " + Type);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public bool Contains(float3 p)
        {
            if (!AABB.Contains(p))
                return false;

            switch (Type)
            {
                case ColliderType.AABB:
                    return true;
                case ColliderType.Sphere:
                    return math.distancesq(p, AABB.Center) <= RadiusSqr;
                case ColliderType.Torus:
                {
                    var d = math.distancesq(p, AABB.Center);
                    return d <= RadiusSqr && d >= TorusMinSqr;
                }
                case ColliderType.TorusCone:
                {
                    var d = math.distancesq(p, AABB.Center);
                    if (d <= RadiusSqr && d >= TorusMinSqr)
                    {
                        var dir = math.normalize(p - AABB.Center);
                        var angle = math.acos(math.dot(dir, ConeDir));
                        return angle <= ConeAngle;
                    }

                    return false;
                }
                default:
                    throw new NotImplementedException("Unimplemented collider type");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Collider(AABB aabb, ColliderType type, float a = 0, float b = 0, float c = 0, float d = 0, float e = 0, float f = 0) : this()
        {
            AABB = aabb;
            Type = type;
            _A = a;
            _B = b;
            _C = c;
            _D = d;
            _E = e;
            _F = f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        Collider(AABB aabb, ColliderType type, float radiusSqr, float torusMinSqr, float coneAngle, float3 coneDir) : this()
        {
            AABB = aabb;
            Type = type;
            RadiusSqr = radiusSqr;
            TorusMinSqr = torusMinSqr;
            ConeAngle = coneAngle;
            ConeDir = coneDir;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Collider DefaultAABB(float radius)
        {
            return new Collider(new AABB(-radius, radius), ColliderType.AABB);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Collider Sphere(float radius)
        {
            return new Collider(new AABB(-radius, radius), ColliderType.Sphere, radius * radius);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Collider Torus(float radiusMin, float radiusMax)
        {
            return new Collider(new AABB(-radiusMax, radiusMax), ColliderType.Torus, radiusMax * radiusMax, radiusMin * radiusMin);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Collider TorusCone(float radiusMin, float radiusMax, float coneAngle, float3 coneDir)
        {
            return new Collider(new AABB(-radiusMax, radiusMax), ColliderType.TorusCone, radiusMax * radiusMax, radiusMin * radiusMin, coneAngle, coneDir);
        }

        public void DebugDraw(CommandBuilder draw, Color color)
        {
            using (draw.WithLineWidth(2, false))
                switch (Type)
                {
                    case ColliderType.AABB:
                        draw.WireSphere(Center, Radius, color*new Color(0, 1f, 0.3f, 0.5f));
                        draw.WireBox(Center, new float3(Radius * 2), color*new Color(0, 1f, 0.3f, 0.5f));
                        break;

                    case ColliderType.Sphere:
                        draw.WireSphere(Center, Radius, color*new Color(0, 1f, 0.3f, 0.5f));
                        draw.WireBox(Center, new float3(Radius * 2), color*new Color(0, 1f, 0.3f, 0.2f));
                        break;

                    case ColliderType.Torus:
                        draw.WireSphere(Center, Radius, color*new Color(0, 1f, 0.3f, 0.5f));
                        draw.WireSphere(Center, TorusMin, color*new Color(1, 0f, 0.3f, 0.5f));
                        draw.WireBox(Center, new float3(Radius * 2), color*new Color(0, 1f, 0.3f, 0.2f));
                        break;

                    case ColliderType.TorusCone:
                        draw.WireSphere(Center, Radius, color*new Color(0, 1f, 0.3f, 0.2f));
                        draw.WireSphere(Center, TorusMin, color*new Color(1, 0f, 0.3f, 0.2f));
                        draw.WireBox(Center, new float3(Radius * 2), color*new Color(0, 1f, 0.3f, 0.2f));

                        // Draw an arrow pointing in the direction of the cone, from the TorusMin to the Radius
                        draw.Arrow((float3)Center + ConeDir * TorusMin, (float3)Center + ConeDir * Radius, math.up(), 0.1f, color*new Color(0, 1f, 0.3f, 0.5f));

                        // Draw a circle at the end of the arrow, with a radius based on the cone angle
                        // The cone is a 'slice' of a sphere, so the circle radius rests on the spheres surface.
                        // The min radius is 0, the max radius is 'Radius'. The circle should also be 'inset' from the edge of the sphere based on the angle.
                        var outerCircleShift = (Radius * Mathf.Cos(ConeAngle));
                        var outerCircleCenter = ConeDir * outerCircleShift;
                        var outerCircleRadius = Radius * Mathf.Sin(ConeAngle);
                        draw.Circle((float3)Center + outerCircleCenter, ConeDir * math.sign(outerCircleShift), outerCircleRadius, color*new Color(0, 1f, 0.3f, 0.5f));

                        // Also draw a circle at the start of the arrow
                        var innerCircleShift = (TorusMin * Mathf.Cos(ConeAngle));
                        var innerCircleCenter = ConeDir * innerCircleShift;
                        var innerCircleRadius = TorusMin * Mathf.Sin(ConeAngle);
                        draw.Circle((float3)Center + innerCircleCenter, ConeDir * math.sign(innerCircleShift), innerCircleRadius, color*new Color(1f, 0.3f, 0.3f, 0.5f));

                        // Now draw arrows connecting the two circles
                        var steps = 8;
                        for (int i = 0; i < steps; i++)
                        {
                            var angle = (i / (float)steps) * math.PI * 2;
                            var circleDir = new float3(math.cos(angle), math.sin(angle), 0);
                            var rot = quaternion.LookRotationSafe(circleDir, ConeDir);
                            var innerPoint = (float3)Center + innerCircleCenter + math.mul(rot, new float3(innerCircleRadius, 0, 0));
                            var outerPoint = (float3)Center + outerCircleCenter + math.mul(rot, new float3(outerCircleRadius, 0, 0));
                            draw.Line(innerPoint, outerPoint, color*new Color(0, 1f, 0.3f, 0.5f));
                        }

                        break;
                }
        }
    }

    public struct SurvivorProjectileTag : IComponentData
    {
    }

    public struct EnemyProjectileTag : IComponentData
    {
    }
}