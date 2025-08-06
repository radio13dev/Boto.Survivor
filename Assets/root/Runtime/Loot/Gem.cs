using System;
using BovineLabs.Saving;
using Unity.Entities;

[Serializable]
public struct Gem
{
    public const int k_GemsPerRing = 6;
    /// <summary>
    /// Used by the client UI to identify the gem.
    /// </summary>
    /// <remarks>
    /// Please don't generate more than 4 billion gems ♥
    /// </remarks>
    [NonSerialized] uint _clientId;
    static uint _nextClientId = 1;
    public uint ClientId
    {
        get
        {
            if (_clientId == 0) _clientId = _nextClientId++;
            return _clientId;
        }
    }
}

[Save]
public struct InventoryGem : IBufferElementData
{
    public Gem Gem;
}
[Save]
public struct EquippedGem : IBufferElementData
{
    public bool Equipped;
    public Gem Gem;
}