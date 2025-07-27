using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

public static class StatExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static bool IsProjectile(this RingPrimaryEffect primaryEffect)
    {
        switch (primaryEffect)
        {
            case RingPrimaryEffect.Projectile_Ring:
            case RingPrimaryEffect.Projectile_NearestRapid:
                return true;
            default:
                return false;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static bool IsTimed(this RingPrimaryEffect primaryEffect)
    {
        switch (primaryEffect)
        {
            case RingPrimaryEffect.Projectile_Ring:
            case RingPrimaryEffect.Projectile_NearestRapid:
                return true;
            default:
                return false;
        }
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static float GetCooldown(this RingPrimaryEffect primaryEffect, float modifier)
    {
        float baseCD = 1;
        switch (primaryEffect)
        {
            case RingPrimaryEffect.Projectile_Ring:
                baseCD = 1f;
                goto default;
            case RingPrimaryEffect.Projectile_NearestRapid:
                baseCD = 0.2f;
                goto default;
            default:
                return baseCD*(modifier >= 0 ? 1/(1 + modifier) : 1/modifier);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static float GetProjectileSpeed(this RingPrimaryEffect primaryEffect, float modifier)
    {
        float baseSpeed = 10f;
        switch (primaryEffect)
        {
            case RingPrimaryEffect.Projectile_Ring:
                baseSpeed = 10f;
                goto default;
            case RingPrimaryEffect.Projectile_NearestRapid:
                baseSpeed = 20f;
                goto default;
            default:
                return (modifier >= 0 ? 1/(1 + modifier) : 1/modifier) * baseSpeed;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static float GetProjectileDuration(this RingPrimaryEffect primaryEffect, float modifier)
    {
        switch (primaryEffect)
        {
            case RingPrimaryEffect.Projectile_Ring:
            case RingPrimaryEffect.Projectile_NearestRapid:
                return (modifier >= 0 ? 1/(1 + modifier) : 1/modifier) * 5f;
            default:
                return -1;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static float GetProjectileDamage(this RingPrimaryEffect primaryEffect, float modifier)
    {
        switch (primaryEffect)
        {
            case RingPrimaryEffect.Projectile_Ring:
            case RingPrimaryEffect.Projectile_NearestRapid:
                return (modifier >= 0 ? 1/(1 + modifier) : 1/modifier) * 10f;
            default:
                return -1;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static float GetProjectileSize(this RingPrimaryEffect primaryEffect, float modifier)
    {
        switch (primaryEffect)
        {
            case RingPrimaryEffect.Projectile_Ring:
            case RingPrimaryEffect.Projectile_NearestRapid:
                return (modifier >= 0 ? 1/(1 + modifier) : 1/modifier) * 1f;
            default:
                return -1;
        }
    }
}