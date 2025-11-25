using System;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;

[Save]
public struct OwnedProjectiles : IBufferElementData
{
    public NetworkId NetworkId;
    public ProjectileKey Key;
}

[Serializable]
public struct ProjectileKey
{
    public RingPrimaryEffect PrimaryEffect;
    public byte Tier;
    public byte Index;

    public ProjectileKey(RingPrimaryEffect effect, byte tier, byte index)
    {
        PrimaryEffect = effect;
        Tier = tier;
        Index = index;
    }
}

[Save]
public struct OwnedProjectile : IComponentData, IEnableableComponent
{
    public int PlayerId;
    public ProjectileKey Key;
}


[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
[UpdateBefore(typeof(RingSystem))]
public partial struct OwnedProjectileSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        new Job()
        {
            OwnedProjectilesLookup = SystemAPI.GetBufferLookup<OwnedProjectiles>(),
            Players = SystemAPI.GetSingletonBuffer<PlayerControlledLink>(true)
        }.Schedule();
    }
    
    partial struct Job : IJobEntity
    {
        public BufferLookup<OwnedProjectiles> OwnedProjectilesLookup;
        [ReadOnly] public DynamicBuffer<PlayerControlledLink> Players;
        
        public void Execute(EnabledRefRW<OwnedProjectile> ownedState, ref OwnedProjectile owned, in NetworkId networkId)
        {
            ownedState.ValueRW = false;
            
            if (owned.PlayerId < 0 || owned.PlayerId >= Players.Length) return;
            var player = Players[owned.PlayerId].Value;
            if (!OwnedProjectilesLookup.HasBuffer(player)) return;
            
            OwnedProjectilesLookup[player].Add(new OwnedProjectiles(){ NetworkId = networkId, Key = owned.Key });
        }
    }
}

