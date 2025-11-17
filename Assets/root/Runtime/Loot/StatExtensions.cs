using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

public static class StatExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static bool IsTimed(this RingPrimaryEffect primaryEffect)
    {
        if ((primaryEffect & RingPrimaryEffect.Projectile_Ring) != 0) return true;
        if ((primaryEffect & RingPrimaryEffect.Projectile_NearestRapid) != 0) return true;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Seeker) != 0) return true;
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static bool IsDoT(this RingPrimaryEffect primaryEffect)
    {
        if ((primaryEffect & RingPrimaryEffect.Projectile_Orbit) != 0) return true;
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static bool IsPersistent(this RingPrimaryEffect primaryEffect)
    {
        if ((primaryEffect & RingPrimaryEffect.Projectile_Orbit) != 0) return true;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Seeker) != 0) return true;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Melee) != 0) return true;
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static float GetCooldown(this RingPrimaryEffect primaryEffect, float modifier)
    {
        float baseCD = 1;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Ring) != 0) baseCD = 1f;
        if ((primaryEffect & RingPrimaryEffect.Projectile_NearestRapid) != 0) baseCD = 0.2f;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Seeker) != 0) baseCD = 5f;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Melee) != 0) baseCD = 2f;
        return baseCD*(modifier >= 0 ? 1/(1 + modifier) : 1/modifier);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static float GetProjectileSpeed(this RingPrimaryEffect primaryEffect, float modifier)
    {
        float baseSpeed = 10f;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Ring) != 0) baseSpeed = 10f;
        if ((primaryEffect & RingPrimaryEffect.Projectile_NearestRapid) != 0) baseSpeed = 20f;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Seeker) != 0) baseSpeed = 30f;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Orbit) != 0) baseSpeed = 30f;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Melee) != 0) baseSpeed = 30f;
        return (modifier >= 0 ? 1/(1 + modifier) : 1/modifier) * baseSpeed;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static float GetProjectileDuration(this RingPrimaryEffect primaryEffect, float modifier)
    {
        float baseDuration = 1f;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Ring) != 0) baseDuration = 2f;
        if ((primaryEffect & RingPrimaryEffect.Projectile_NearestRapid) != 0) baseDuration = 1f;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Seeker) != 0) baseDuration = 19f;
        return (modifier >= 0 ? 1/(1 + modifier) : 1/modifier) * baseDuration;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static int GetProjectileDamage(this RingPrimaryEffect primaryEffect, int modifier)
    {
        int baseDamage = 100;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Ring) != 0) baseDamage = 100;
        if ((primaryEffect & RingPrimaryEffect.Projectile_NearestRapid) != 0) baseDamage = 100;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Seeker) != 0) baseDamage = 100;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Orbit) != 0) baseDamage = 100;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Melee) != 0) baseDamage = 100;
        return baseDamage + modifier;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static float GetProjectileSize(this RingPrimaryEffect primaryEffect, float modifier)
    {
        float baseSize = 1f;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Ring) != 0) baseSize = 1f;
        if ((primaryEffect & RingPrimaryEffect.Projectile_NearestRapid) != 0) baseSize = 1f;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Seeker) != 0) baseSize = 1f;
        if ((primaryEffect & RingPrimaryEffect.Projectile_Melee) != 0) baseSize = 1f;
        return (modifier >= 0 ? 1/(1 + modifier) : 1/modifier) * baseSize;
    }
}