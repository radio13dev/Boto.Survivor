using System;
using System.Collections.Generic;
using BovineLabs.Saving;
using Collisions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[Save]
public struct Projectile : IComponentData
{
    public int Damage;
    
    public Projectile(int damage)
    {
        Damage = damage;
    }

    public const float PerFrameDamageMod = 1f/30f;

    public static void Setup(in int Key, ref EntityCommandBuffer.ParallelWriter ecb, in Entity projectileE, in CompiledStats compiledStats, ref Random random)
    {
        var (damageWithMods, baseDamage, crit, chain, cut, degenerate, subdivide, decimate, dissolve, poke) = compiledStats.RollDamage(ref random);
                            
        ecb.SetComponent(Key, projectileE, new Projectile(damageWithMods));
        if (crit) ecb.SetComponent(Key, projectileE, crit); else ecb.SetComponentEnabled<Crit>(Key, projectileE, false);
        if (chain) ecb.SetComponent(Key, projectileE, chain); else ecb.SetComponentEnabled<Chain>(Key, projectileE, false);
        if (cut) ecb.SetComponent(Key, projectileE, cut); else ecb.SetComponentEnabled<Cut>(Key, projectileE, false);
        if (degenerate) ecb.SetComponent(Key, projectileE, degenerate); else ecb.SetComponentEnabled<Degenerate>(Key, projectileE, false);
        if (subdivide) ecb.SetComponent(Key, projectileE, subdivide); else ecb.SetComponentEnabled<Subdivide>(Key, projectileE, false);
        if (decimate) ecb.SetComponent(Key, projectileE, decimate); else ecb.SetComponentEnabled<Decimate>(Key, projectileE, false);
        if (dissolve) ecb.SetComponent(Key, projectileE, dissolve); else ecb.SetComponentEnabled<Dissolve>(Key, projectileE, false);
        if (poke) ecb.SetComponent(Key, projectileE, poke); else ecb.SetComponentEnabled<Poke>(Key, projectileE, false);
    }
}

[Save]
public struct Pierce : IComponentData
{
    public byte Value;
    public static readonly Pierce Infinite = new Pierce(){ Value = byte.MaxValue };
    public bool IsInfinite => Value == byte.MaxValue;
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