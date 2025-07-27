using BovineLabs.Saving;
using Unity.Entities;

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