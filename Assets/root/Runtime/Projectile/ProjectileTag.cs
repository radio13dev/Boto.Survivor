using System;
using System.Collections.Generic;
using BovineLabs.Saving;
using Collisions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Collider = Collisions.Collider;
using Random = Unity.Mathematics.Random;


[Save]
public struct Projectile : IComponentData
{
    public int Damage;
    public bool IsDoT;

    public Projectile(int damage, bool isDoT)
    {
        Damage = (int)math.ceil(damage*Projectile.PerFrameDamageMod);
        IsDoT = isDoT;
    }

    public const int InvPerFrameDamageMod = 30;
    public const float PerFrameDamageMod = 1f / InvPerFrameDamageMod;

    public static void Setup(ref EntityCommandBuffer ecb, ref Random random, in Entity projectileE,
        in CompiledStats compiledStats,
        in PlayerControlled playerId, in RingPrimaryEffect effect, in byte tier, in byte projSpawnIt, in double destroyTime)
    {
        var (damageWithMods, baseDamage, crit, chain, cut, degenerate, subdivide, decimate, dissolve, poke) = compiledStats.RollDamage(ref random);

        ecb.SetComponent(projectileE, new Projectile(damageWithMods, effect.IsDoT()));
        if (crit) ecb.SetComponent(projectileE, crit);
        else ecb.SetComponentEnabled<Crit>(projectileE, false);
        if (chain) ecb.SetComponent(projectileE, chain);
        else ecb.SetComponentEnabled<Chain>(projectileE, false);
        if (cut) ecb.SetComponent(projectileE, cut);
        else ecb.SetComponentEnabled<Cut>(projectileE, false);
        if (degenerate) ecb.SetComponent(projectileE, degenerate);
        else ecb.SetComponentEnabled<Degenerate>(projectileE, false);
        if (subdivide) ecb.SetComponent(projectileE, subdivide);
        else ecb.SetComponentEnabled<Subdivide>(projectileE, false);
        if (decimate) ecb.SetComponent(projectileE, decimate);
        else ecb.SetComponentEnabled<Decimate>(projectileE, false);
        if (dissolve) ecb.SetComponent(projectileE, dissolve);
        else ecb.SetComponentEnabled<Dissolve>(projectileE, false);
        if (poke) ecb.SetComponent(projectileE, poke);
        else ecb.SetComponentEnabled<Poke>(projectileE, false);

        ecb.SetComponent(projectileE, ProjectileLoopTrigger.Empty);
        ecb.SetComponent(projectileE, new DestroyAtTime() { DestroyTime = destroyTime });
        ecb.SetComponent(projectileE, new OwnedProjectile() { PlayerId = playerId.Index, Key = new ProjectileKey(effect, tier, projSpawnIt) });
    }
    
    public static void Setup(in int Key, ref EntityCommandBuffer.ParallelWriter ecb, ref Random random, in Entity projectileE,
        in CompiledStats compiledStats,
        in PlayerControlled playerId, in RingPrimaryEffect effect, in byte tier, in byte projSpawnIt, in double destroyTime)
    {
        var (damageWithMods, baseDamage, crit, chain, cut, degenerate, subdivide, decimate, dissolve, poke) = compiledStats.RollDamage(ref random);

        ecb.SetComponent(Key, projectileE, new Projectile(damageWithMods, effect.IsDoT()));
        if (crit) ecb.SetComponent(Key, projectileE, crit);
        else ecb.SetComponentEnabled<Crit>(Key, projectileE, false);
        if (chain) ecb.SetComponent(Key, projectileE, chain);
        else ecb.SetComponentEnabled<Chain>(Key, projectileE, false);
        if (cut) ecb.SetComponent(Key, projectileE, cut);
        else ecb.SetComponentEnabled<Cut>(Key, projectileE, false);
        if (degenerate) ecb.SetComponent(Key, projectileE, degenerate);
        else ecb.SetComponentEnabled<Degenerate>(Key, projectileE, false);
        if (subdivide) ecb.SetComponent(Key, projectileE, subdivide);
        else ecb.SetComponentEnabled<Subdivide>(Key, projectileE, false);
        if (decimate) ecb.SetComponent(Key, projectileE, decimate);
        else ecb.SetComponentEnabled<Decimate>(Key, projectileE, false);
        if (dissolve) ecb.SetComponent(Key, projectileE, dissolve);
        else ecb.SetComponentEnabled<Dissolve>(Key, projectileE, false);
        if (poke) ecb.SetComponent(Key, projectileE, poke);
        else ecb.SetComponentEnabled<Poke>(Key, projectileE, false);

        ecb.SetComponent(Key, projectileE, ProjectileLoopTrigger.Empty);
        ecb.SetComponent(Key, projectileE, new DestroyAtTime() { DestroyTime = destroyTime });
        ecb.SetComponent(Key, projectileE, new OwnedProjectile() { PlayerId = playerId.Index, Key = new ProjectileKey(effect, tier, projSpawnIt) });
    }

    public static void SetSpeed(ref EntityCommandBuffer ecb, in Entity projectileE, in CompiledStats compiledStats, float baseSpeed)
    {
        ecb.SetComponent(projectileE, new MovementSettings() { Speed = compiledStats.CompiledStatsTree.ProjectileSpeed * baseSpeed });
    }
    public static void SetSpeed(in int Key, ref EntityCommandBuffer.ParallelWriter ecb, in Entity projectileE, in CompiledStats compiledStats, float baseSpeed)
    {
        ecb.SetComponent(Key, projectileE, new MovementSettings() { Speed = compiledStats.CompiledStatsTree.ProjectileSpeed * baseSpeed });
    }

    public static void SetSurfaceSpeed(in int Key, ref EntityCommandBuffer.ParallelWriter ecb, in Entity projectileE, in CompiledStats compiledStats, float baseSpeed)
    {
        ecb.SetComponent(Key, projectileE, new SurfaceMovement() { PerFrameVelocity = new float3(compiledStats.CompiledStatsTree.ProjectileSpeed * baseSpeed, 0, 0) });
    }

    public static void SetPierce(in int Key, ref EntityCommandBuffer.ParallelWriter ecb, in Entity projectileE, in CompiledStats compiledStats)
    {
        ecb.SetComponent(Key, projectileE, new Pierce() { Value = compiledStats.CompiledStatsTree.PierceCount });
    }

    public static void SetIgnoredEntities(in int Key, ref EntityCommandBuffer.ParallelWriter ecb, in Entity projectileE,
        in NativeList<(Entity e, NetworkId id, Collider c)> ignoreNearbyBuffer)
    {
        if (ignoreNearbyBuffer.IsCreated)
            ecb.SetBuffer<ProjectileIgnoreEntity>(Key, projectileE).AddRange(ignoreNearbyBuffer.AsArray().Reinterpret<ProjectileIgnoreEntity>());
    }
}

[Save]
public struct Pierce : IComponentData
{
    public byte Value;
    public static readonly Pierce Infinite = new Pierce() { Value = byte.MaxValue };
    public bool IsInfinite => Value == byte.MaxValue;
}

// @formatter:off
[Save] public struct Crit : IComponentData, IEnableableComponent {          public Crit(byte value) { Value = value; }          public byte Value; public static implicit operator bool(Crit v)       => v.Value > 0; }
[Save] public struct Chain : IComponentData, IEnableableComponent {         public Chain(byte value) { Value = value; }         public byte Value; public static implicit operator bool(Chain v)      => v.Value > 0;  }
[Save] public struct Cut : IComponentData, IEnableableComponent {           public Cut(byte value) { Value = value; }           public byte Value; public static implicit operator bool(Cut v)        => v.Value > 0;  }
[Save] public struct Degenerate : IComponentData, IEnableableComponent {    public Degenerate(byte value) { Value = value; }    public byte Value; public static implicit operator bool(Degenerate v) => v.Value > 0;  }
[Save] public struct Subdivide : IComponentData, IEnableableComponent {     public Subdivide(byte value) { Value = value; }     public byte Value; public static implicit operator bool(Subdivide v)  => v.Value > 0;  }
[Save] public struct Decimate : IComponentData, IEnableableComponent {      public Decimate(byte value) { Value = value; }      public byte Value; public static implicit operator bool(Decimate v)   => v.Value > 0;  }
[Save] public struct Dissolve : IComponentData, IEnableableComponent {      public Dissolve(byte value) { Value = value; }      public byte Value; public static implicit operator bool(Dissolve v)   => v.Value > 0;  }
[Save] public struct Poke : IComponentData, IEnableableComponent {          public Poke(byte value) { Value = value; }          public byte Value; public static implicit operator bool(Poke v)       => v.Value > 0;  }
// @formatter:on

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