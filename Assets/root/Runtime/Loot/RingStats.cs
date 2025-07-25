using BovineLabs.Saving;
using Unity.Entities;

[Save]
public struct Ring : IBufferElementData
{
    public double LastActivateTime;

    public RingStats Stats;
}

[Save]
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
}


public enum RingPrimaryEffect
{
    None,
    Projectile_Ring
}

public enum RingSecondaryEffect
{
    Trigger_Left
}