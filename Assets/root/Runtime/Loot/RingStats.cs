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
    public bool IsValid => PrimaryEffect != RingPrimaryEffect.None && PrimaryEffect != RingPrimaryEffect.Length;
    public RingPrimaryEffect PrimaryEffect;

    public string GetTitleString()
    {
        switch (PrimaryEffect)
        {
            case RingPrimaryEffect.Projectile_Ring:
                return "Ring of Projectiles";
            case RingPrimaryEffect.Projectile_NearestRapid:
                return "Rapid Ring";
            default:
                return "???";
        }
    }

    public string GetDescriptionString()
    {
        switch (PrimaryEffect)
        {
            case RingPrimaryEffect.Projectile_Ring:
                return "Launches 8 projectiles out in a ring around you";
            case RingPrimaryEffect.Projectile_NearestRapid:
                return "Rapidly fires at the nearest target";
            default:
                return "???";
        }
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