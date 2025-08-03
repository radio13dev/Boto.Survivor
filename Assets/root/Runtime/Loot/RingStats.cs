using System;
using BovineLabs.Saving;
using Unity.Entities;

[Save]
public struct Ring : IBufferElementData
{
    public const int k_RingCount = 8;

    public double LastActivateTime;

    public RingStats Stats;
}

[Save]
[Serializable]
public struct RingStats : IComponentData
{
    // Primary effect
    public RingPrimaryEffect PrimaryEffect;
    public float PrimaryEffectValue;

    // Projectile mods
    public float Size;
    public float Damage;
    public float ProjectileSpeed;
    public float ProjectileDuration;

    // Character mods
    public float Speed;
    public float Regen;
    public float MaxHealth;
    public int ProjectileCount;
    public float ProjectileRate;

    // Extra effect
    public RingSecondaryEffect SecondaryEffect;
    public float SecondaryEffectValue;

    public void Add(RingStats stats)
    {
        Size += stats.Size;
        Damage += stats.Damage;
        ProjectileSpeed += stats.ProjectileSpeed;
        ProjectileDuration += stats.ProjectileDuration;
        Speed += stats.Speed;
        Regen += stats.Regen;
        MaxHealth += stats.MaxHealth;
        ProjectileCount += stats.ProjectileCount;
        ProjectileRate += stats.ProjectileRate;
    }
}


public enum RingPrimaryEffect
{
    None,
    Projectile_Ring,
    Projectile_NearestRapid,
    
    Length
}

public enum RingSecondaryEffect
{
    Trigger_Left
}