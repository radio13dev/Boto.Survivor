using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using BovineLabs.Saving;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using AABB = NativeTrees.AABB;

namespace Collisions
{
    [Save]
    public struct TorusMin : IComponentData
    {
        public float Value;
        
        public TorusMin(float value)
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

    [BurstCompile]
    [Save]
    public struct Collider : IComponentData, IEnableableComponent
    {
        public NativeTrees.AABB Value;
        public float TorusMin;

        public Collider(AABB value)
        {
            Value = value;
            TorusMin = 0;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public NativeTrees.AABB Add(LocalTransform t)
        {
            return new NativeTrees.AABB(Value.min*t.Scale + t.Position, Value.max*t.Scale + t.Position);
        }

        public static Collider Torus(float radMin, int radMax)
        {
            return new Collider()
            {
                Value = new AABB(-radMax, radMax),
                TorusMin = radMin
            };
        }
    }

    public struct SurvivorProjectileTag : IComponentData { }

    public struct EnemyProjectileTag : IComponentData { }
}