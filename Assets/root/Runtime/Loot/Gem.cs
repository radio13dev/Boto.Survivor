using System;
using System.Collections.Generic;
using BovineLabs.Saving;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

[Serializable]
public struct Gem : IEquatable<Gem>, IComparable<Gem>
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
        Homing,
        Pierce,
        Length
    }

    public bool IsValid => GemType != Type.None;

    public Type GemType;
    public int Size;

    public bool Equals(Gem other)
    {
        return GemType == other.GemType && Size == other.Size;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int)GemType, Size);
    }
    
    public int CompareTo(Gem other)
    {
        var gemTypeComparison = GemType.CompareTo(other.GemType);
        if (gemTypeComparison != 0) return gemTypeComparison;
        return Size.CompareTo(other.Size);
    }

    #region Equality

    public override bool Equals(object obj)
    {
        return obj is Gem other && Equals(other);
    }

    public static bool operator ==(Gem left, Gem right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Gem left, Gem right)
    {
        return !left.Equals(right);
    }

    #endregion

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

    public static Gem Generate(ref Random random)
    {
        return new Gem()
        {
            GemType = (Type)random.NextInt(1, (int)Type.Length)
        };
    }

    public static void SetupEntity(Entity entity, ref Random random, ref EntityCommandBuffer ecb, LocalTransform transform, Movement movement, Gem gem, DynamicBuffer<GameManager.GemVisual> gemVisuals)
    {
        ecb.SetComponent(entity, new GemDrop()
        {
            Gem = gem
        });
        ecb.SetSharedComponent(entity, new InstancedResourceRequest(gemVisuals[(int)gem.GemType].InstancedResourceIndex));
        
        ecb.SetComponent(entity, transform);
        
        var newDropInertia = new RotationalInertia();
        newDropInertia.Set(random.NextFloat3(), 1);
        ecb.SetComponent(entity, newDropInertia);
        
        
        var newDropMovement = new Movement();
        newDropMovement.Velocity = movement.Velocity + (math.length(movement.Velocity)/2 + 1) * random.NextFloat3();
        ecb.SetComponent(entity, newDropMovement);
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