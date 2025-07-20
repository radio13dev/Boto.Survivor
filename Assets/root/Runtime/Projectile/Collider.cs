using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using BovineLabs.Saving;
using NativeTrees;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using AABB = NativeTrees.AABB;

namespace Collisions
{
    /// <summary>
    /// Projectile collision is predicted, only after all movement is done
    /// </summary>
    [UpdateAfter(typeof(MovementSystemGroup))]
    public partial class CollisionSystemGroup : ComponentSystemGroup
    {
    }

    [BurstCompile]
    [Save]
    public struct Collider : IComponentData
    {
        public NativeTrees.AABB Value;

        public Collider(AABB value)
        {
            Value = value;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public NativeTrees.AABB Add(float3 offset)
        {
            return new NativeTrees.AABB(Value.min + offset, Value.max + offset);
        }
    }

    public struct SurvivorProjectileTag : IComponentData
    {
        public const Team Team = global::Team.Survivor;
    }

    public struct EnemyProjectileTag : IComponentData
    {
        public const Team Team = global::Team.Enemy;
    }
}