using System;
using System.Collections.Generic;
using BovineLabs.Saving;
using Collisions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[Save]
public struct Projectile : IComponentData
{
    public float Damage;
}

[Save]
public struct ProjectileHit : IComponentData, IEnableableComponent
{
}

[Save]
public struct ProjectileHitEntity : IBufferElementData
{
    public NetworkId Value;

    public ProjectileHitEntity(NetworkId value)
    {
        Value = value;
    }
}

[Save]
public struct ProjectileIgnoreEntity : IBufferElementData
{
    public NetworkId Value;

    public ProjectileIgnoreEntity(NetworkId value)
    {
        Value = value;
    }
}

[UpdateBefore(typeof(CollisionSystemGroup))]
[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
public partial class ProjectileSystemGroup : ComponentSystemGroup
{
}