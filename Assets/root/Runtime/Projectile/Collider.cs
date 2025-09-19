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
using Vella.UnityNativeHull;
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
        MeshCollider,
    }

    public struct LazyCollider
    {
        public Collider Source;
        public LocalTransform Transform;
        
        public Collider Adjusted
        {
            get
            {
                if (!_applied)
                {
                    _adjusted = Source.Apply(Transform);
                    _applied = true;
                }
                return _adjusted;
            }
        }
        Collider _adjusted;
        bool _applied;

        private LazyCollider(Collider source, LocalTransform transform)
        {
            Source = source;
            Transform = transform;
            
            _adjusted = default;
            _applied = false;
        }

        public static implicit operator LazyCollider((Collider, LocalTransform) source)
        {
            return new LazyCollider(source.Item1, source.Item2);
        }
        public static implicit operator LazyCollider((LocalTransform, Collider) source)
        {
            return new LazyCollider(source.Item2, source.Item1);
        }
        
        public static implicit operator Collider(LazyCollider source) => source.Adjusted;
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
        [SerializeField] [FieldOffset(52)] public readonly float _G;
        [SerializeField] [FieldOffset(56)] public readonly float _H;
        [SerializeField] [FieldOffset(60)] public readonly float _I;

        [FieldOffset(28)] public readonly float RadiusSqr;
        [FieldOffset(32)] public readonly float TorusMinSqr;
        [FieldOffset(36)] public readonly float ConeAngle;
        [FieldOffset(40)] public readonly float3 ConeDir;
        
        [FieldOffset(28)] public readonly int MeshPtr; // 28
        [FieldOffset(32)] public readonly LocalTransform MeshTransform; //32,36,40 + 44 + 48,52,56,60
        
        // Capsule
        [FieldOffset(32)] public readonly float3 P1; //32,36,40
        [FieldOffset(44)] public readonly float3 P2; //44,48,52

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
                case ColliderType.MeshCollider:
                    return new Collider(aabb, ColliderType.MeshCollider, MeshPtr, transform);   
                default:
                    throw new NotImplementedException("Unimplemented collider type");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public unsafe bool Overlaps(in Collider other)
        {
            if (!AABB.Overlaps(other.AABB))
                return false;

            switch (Type)
            {
                case ColliderType.AABB:
                    switch (other.Type)
                    {
                        case ColliderType.AABB: // AABB + AABB
                            return true;
                        case ColliderType.Sphere:
                            return math.distancesq(AABB.ClosestPoint(other.Center), other.Center) < other.RadiusSqr;
                        case ColliderType.Torus: // AABB + Torus
                        {
                            var c = AABB.ClosestPoint(other.Center);
                            var d = math.distancesq(c, other.Center);
                            return d <= other.RadiusSqr && math.distance(c + AABB.Size*math.sign(Center-c), other.Center) >= other.TorusMinSqr;
                        }
                        case ColliderType.TorusCone: // AABB + TorusCone
                        {
                            var c = AABB.ClosestPoint(other.Center);
                            var d = math.distancesq(c, other.Center);
                            if (d <= other.RadiusSqr && math.distance(c + AABB.Size*math.sign(Center-c), other.Center) >= other.TorusMinSqr)
                            {
                                var dir = math.normalize(other.Center - AABB.Center);
                                var angle = math.acos(math.dot(dir, ConeDir));
                                return angle <= ConeAngle;
                            }
                            return false;
                        }
                        case ColliderType.MeshCollider: // AABB + MeshCollider
                        {
                            var p = Center + Radius*math.normalize(other.Center - Center);
                            return HullCollision.Contains(new RigidTransform(other.MeshTransform.Rotation, 0), ((NativeHull*)NativeHullManager.m_Hulls.Data)[other.MeshPtr], (p-other.MeshTransform.Position)/other.MeshTransform.Scale);
                        }
                        default:
                            throw new NotImplementedException("Unimplemented collider collision");
                    }
                case ColliderType.Sphere:
                    switch (other.Type)
                    {
                        case ColliderType.AABB: // Sphere + AABB
                            return other.Overlaps(this);
                        case ColliderType.Sphere: // Sphere + Sphere
                            return math.distancesq(Center, other.Center) < math.square(Radius + other.Radius);
                        case ColliderType.Torus: // Sphere + Torus
                        {
                            var d = math.distancesq(Center, other.Center);
                            return d <= math.square(Radius + other.Radius) && d >= math.square(other.TorusMin - Radius);
                        }
                        case ColliderType.TorusCone: // Sphere + TorusCone
                        {
                            var c = AABB.ClosestPoint(other.Center);
                            var d = math.distancesq(c, other.Center);
                            if (d <= other.RadiusSqr && math.distance(c + AABB.Size*math.sign(Center-c), other.Center) >= other.TorusMinSqr)
                            {
                                var dir = math.normalize(other.Center - AABB.Center);
                                var angle = math.acos(math.dot(dir, ConeDir));
                                return angle <= ConeAngle;
                            }
                            return false;
                        }
                        case ColliderType.MeshCollider: // Sphere + MeshCollider
                        {
                            var p = Center + Radius*math.normalize(other.Center - Center);
                            return HullCollision.Contains(new RigidTransform(other.MeshTransform.Rotation, 0), ((NativeHull*)NativeHullManager.m_Hulls.Data)[other.MeshPtr], (p-other.MeshTransform.Position)/other.MeshTransform.Scale);
                        }
                        default:
                            throw new NotImplementedException("Unimplemented collider collision");
                    }
                case ColliderType.Torus:
                    switch (other.Type)
                    {
                        case ColliderType.AABB: // Torus + AABB
                            return other.Overlaps(this);
                        case ColliderType.Sphere: // Torus + Sphere
                            return other.Overlaps(this);
                        case ColliderType.Torus: // Torus + Torus
                        {
                            var d = math.distance(Center, other.Center);
                            if ((d + Radius > other.TorusMin) && (d + TorusMin < other.Radius))
                            {
                                return true;
                            }
                            if ((d + other.Radius > TorusMin) && (d + other.TorusMin < Radius))
                            {
                                Debug.LogError("Fallback collision hit, you were wrong ben!");
                                return true;
                            }
                            return false;
                        }
                        case ColliderType.TorusCone: // Torus + TorusCone
                        {
                            var d = math.distance(Center, other.Center);
                            if (((d + Radius > other.TorusMin) && (d + TorusMin < other.Radius)) || (d + other.Radius > TorusMin) && (d + other.TorusMin < Radius))
                            {
                                var dir = math.normalize(other.Center - AABB.Center);
                                var angle = math.acos(math.dot(dir, ConeDir));
                                return angle <= ConeAngle;
                            }
                            return false;
                        }
                        case ColliderType.MeshCollider: // Torus + MeshCollider
                        {
                            var p = Center + Radius*math.normalize(other.Center - Center);
                            return HullCollision.Contains(new RigidTransform(other.MeshTransform.Rotation, 0), ((NativeHull*)NativeHullManager.m_Hulls.Data)[other.MeshPtr], (p-other.MeshTransform.Position)/other.MeshTransform.Scale);
                        }
                        default:
                            throw new NotImplementedException("Unimplemented collider collision");
                    }
                case ColliderType.TorusCone:
                    switch (other.Type)
                    {
                        case ColliderType.AABB: // TorusCone + AABB
                            return other.Overlaps(this);
                        case ColliderType.Sphere: // TorusCone + Sphere
                            return other.Overlaps(this);
                        case ColliderType.Torus: // TorusCone + Torus
                            return other.Overlaps(this);
                        case ColliderType.TorusCone: // TorusCone + TorusCone
                        {
                            var d = math.distance(Center, other.Center);
                            if (((d + Radius > other.TorusMin) && (d + TorusMin < other.Radius)) || (d + other.Radius > TorusMin) && (d + other.TorusMin < Radius))
                            {
                                var dir = math.normalize(other.Center - AABB.Center);
                                var angle = math.acos(math.dot(dir, ConeDir));
                                return angle <= ConeAngle || angle <= math.acos(math.dot(dir, other.ConeDir));
                            }
                            return false;
                        }
                        case ColliderType.MeshCollider: // TorusCone + MeshCollider
                        {
                            var p = Center + Radius*math.normalize(other.Center - Center);
                            return HullCollision.Contains(new RigidTransform(other.MeshTransform.Rotation, 0), ((NativeHull*)NativeHullManager.m_Hulls.Data)[other.MeshPtr], (p-other.MeshTransform.Position)/other.MeshTransform.Scale);
                        }
                        default:
                            throw new NotImplementedException("Unimplemented collider collision");
                    }
                case ColliderType.MeshCollider:
                {
                    switch (other.Type)
                    {
                        case ColliderType.MeshCollider:
                            return false; // Can't overlap yet
                    }
                    return other.Overlaps(this);
                }
                default:
                    throw new NotImplementedException("Unimplemented collider type");
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe float3 GetPointOnSurface(Collider other)
        {
            var p = other.Center;
            switch (Type)
            {
                default:
                case ColliderType.Sphere:
                {
                    var d = math.distance(p, Center);
                    return math.normalizesafe(p - Center)*(Radius - d);
                }
                case ColliderType.Torus:
                {
                    var d = math.distance(p, Center);
                    if (d >= (TorusMin + Radius) / 2)
                        return math.normalizesafe(p - Center)*(Radius - d);
                    else
                        return math.normalizesafe(p - Center)*(TorusMin - d);
                }
                case ColliderType.MeshCollider:
                {
                    var shift = other.Radius*math.normalize(Center - other.Center);
                    p = other.Center + shift;
                    var pClose = HullCollision.ClosestPoint(new RigidTransform(MeshTransform.Rotation, 0), ((NativeHull*)NativeHullManager.m_Hulls.Data)[MeshPtr], (p-MeshTransform.Position)/MeshTransform.Scale);
                    pClose *= MeshTransform.Scale;
                    pClose += MeshTransform.Position;
                    pClose -= shift;
                    return pClose;
                }
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
        Collider(AABB aabb, ColliderType type, int meshPtr, LocalTransform meshTransform) : this()
        {
            AABB = aabb;
            Type = type;
            MeshPtr = meshPtr;
            MeshTransform = meshTransform;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Collider MeshCollider(DatabaseRef<Mesh, MeshDatabase> meshReference)
        {
            return new Collider(new AABB(-meshReference.Asset.bounds.extents*2, meshReference.Asset.bounds.extents*2), ColliderType.MeshCollider, meshReference.GetAssetIndex(), LocalTransform.Identity);
        }

        public void DebugDraw(CommandBuilder draw, Color color)
        {
            using (draw.WithLineWidth(2, false))
                switch (Type)
                {
                    case ColliderType.AABB:
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

                    case ColliderType.MeshCollider:
                        draw.WireBox(Center, new float3(Radius * 2), color*new Color(0, 1f, 0.3f, 0.5f));
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