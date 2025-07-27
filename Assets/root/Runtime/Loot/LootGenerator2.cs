using BovineLabs.Saving;
using Unity.Entities;
using Unity.Mathematics;

[Save]
public struct LootGenerator2 : IComponentData
{
    public int OptionCount;
    public long Seed;

    public RingStats GetRingStats(int index)
    {
        var random = Random.CreateFromIndex(unchecked((uint)(Seed)));
        for (int i = 0; i < OptionCount; i++)
        {
            RingStats stats = new RingStats();
            
            // TODO: Implement weighting
            stats.PrimaryEffect = (RingPrimaryEffect)random.NextInt((int)RingPrimaryEffect.None + 1, (int)RingPrimaryEffect.Length + 1);
            
            if (i == index) return stats;
        }
        
        return default;
    }
}