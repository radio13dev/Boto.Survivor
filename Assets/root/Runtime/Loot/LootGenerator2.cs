using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BovineLabs.Core.SingletonCollection;
using BovineLabs.Saving;
using Unity.Entities;
using Random = Unity.Mathematics.Random;

[Save]
public struct LootGenerator2 : IComponentData
{
    public int OptionCount;
    public long Seed;

    public RingStats GetRingStats(int index)
    {
        if (index < 0 || index >= OptionCount)
            return default;
        
        return new RingStats()
        {
            PrimaryEffect = RingPrimaryEffect.Projectile_Ring
        };
    }
}