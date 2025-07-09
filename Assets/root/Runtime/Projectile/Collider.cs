using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Collisions
{
    /// <summary>
    /// Projectile collision is predicted, only after all movement is done
    /// </summary>
    [UpdateAfter(typeof(MovementSystemGroup))]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial class CollisionSystemGroup : ComponentSystemGroup
    {
    }

    [GhostComponent]
    [BurstCompile]
    public struct Collider : IComponentData
    {
        public NativeTrees.AABB2D Value;

        public Collider(AABB2D value)
        {
            Value = value;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public NativeTrees.AABB2D Add(float2 offset)
        {
            return new NativeTrees.AABB2D(Value.min + offset, Value.max + offset);
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