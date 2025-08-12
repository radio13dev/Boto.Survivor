using System;
using System.Collections.Generic;
using BovineLabs.Core.Extensions;
using BovineLabs.Saving;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[Serializable]
public struct Gem : IEquatable<Gem>, IComparable<Gem>
{
    public const int k_GemsPerRing = 6;
    
    #region Actual Values

    public bool IsValid => GemType != Type.None;
    public Type GemType;
    public int Size;
    
    public Gem(Type gemType, int size)
    {
        GemType = gemType;
        Size = size;
        _clientId = 0; // Reset client ID for new gem
    }

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
    #endregion

    #region Clientside

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

    public Material Material
    {
        get
        {
            var visuals = Game.ClientGame.World.EntityManager.GetSingletonBuffer<GameManager.GemVisual>(true);
            var instances = Game.ClientGame.World.EntityManager.GetSingletonBuffer<GameManager.InstancedResources>(true);
            return instances[visuals[(int)GemType].InstancedResourceIndex].Instance.Value.Material;
        }
    }
    
    public Mesh Mesh
    {
        get
        {
            var visuals = Game.ClientGame.World.EntityManager.GetSingletonBuffer<GameManager.GemVisual>(true);
            var instances = Game.ClientGame.World.EntityManager.GetSingletonBuffer<GameManager.InstancedResources>(true);
            return instances[visuals[(int)GemType].InstancedResourceIndex].Instance.Value.Mesh;
        }
    }

    #endregion

    public enum Type
    {
        None,
        Multishot,
        Homing,
        Pierce,
        Length
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

    public static void SetupEntity(Entity entity, int playerId, ref Random random, ref EntityCommandBuffer ecb, LocalTransform transform, Movement movement, Gem gem,
        DynamicBuffer<GameManager.GemVisual> gemVisuals)
    {
        ecb.SetComponent(entity, new GemDrop()
        {
            Gem = gem
        });
        ecb.SetSharedComponent(entity, new InstancedResourceRequest(gemVisuals[(int)gem.GemType].InstancedResourceIndex));

        transform.Rotation = random.NextQuaternionRotation();
        ecb.SetComponent(entity, transform);

        var newDropMovement = new Movement();
        TorusMapper.SnapToSurface(transform.Position, 0, out _, out var surfaceNormal);
        var jumpDir = random.NextFloat(1, 2) * PhysicsSettings.s_GemJump.Data * (surfaceNormal + random.NextFloat3Direction() / 2); // movement.Velocity + 
        newDropMovement.Velocity = jumpDir;
        ecb.SetComponent(entity, newDropMovement);

        // Mark who's allowed to collect this
        ecb.SetComponent(entity, new Collectable() { PlayerId = playerId });
        //ecb.SetComponentEnabled<Collectable>(entity, true);
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