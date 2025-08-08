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
    
    public enum Type
    {
        None,
        Multishot,
    }
    
    public bool IsValid => GemType != Type.None;
    
    public Type GemType;
    public int Size;
    
    public Gem(Type gemType, int size)
    {
        GemType = gemType;
        Size = size;
        _clientId = 0; // Reset client ID for new gem
    }

    public string GetTitleString()
    {
        switch (GemType)
        {
            case Type.Multishot:
                return "Multishot Gem";
            default:
                return GemType.ToString() + " Gem (default text)";
        }
    }
}

[Save]
public struct InventoryGem : IBufferElementData
{
    public Gem Gem;

    public InventoryGem(Gem gem)
    {
        _ = gem.ClientId;
        Gem = gem;
    }
}
[Save]
public struct EquippedGem : IBufferElementData
{
    public Gem Gem;
    
    public EquippedGem(Gem gem)
    {
        _ = gem.ClientId;
        Gem = gem;
    }
}