using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Internal;
using BovineLabs.Core.SingletonCollection;
using BovineLabs.Saving.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public readonly struct GameState : IEquatable<GameState>
{
    public readonly string WorldName;
    public readonly Dictionary<(float3, quaternion), EntityState> Entities;
    public GameState(string worldName, Dictionary<(float3, quaternion), EntityState> entities)
    {
        WorldName = worldName;
        this.Entities = entities;
    }
    
    public readonly struct EntityState : IEquatable<EntityState>
    {
        public readonly Movement Movement;
        public readonly StepInput StepInput;
        public readonly Force Force;
        
        public EntityState(EntityManager entityManager, Entity entity)
        {
            Movement = GetOrDefaultComponent<Movement>(entityManager, entity);
            StepInput = GetOrDefaultComponent<StepInput>(entityManager, entity); 
            Force = GetOrDefaultComponent<Force>(entityManager, entity);
        }

        private static T GetOrDefaultComponent<T>(EntityManager entityManager, Entity entity) where T : unmanaged, IComponentData
        {
            if (!entityManager.HasComponent<T>(entity))
                return default;
            return entityManager.GetComponentData<T>(entity);
        }
        
        public bool Equals(EntityState other)
        {
            return Movement.Equals(other.Movement) && StepInput.Equals(other.StepInput) && Force.Equals(other.Force);
        }

        public override bool Equals(object obj)
        {
            return obj is EntityState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Movement, StepInput, Force);
        }

        public static bool operator ==(EntityState left, EntityState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EntityState left, EntityState right)
        {
            return !left.Equals(right);
        }
    }

    public static GameState Compile(Game game)
    {
        var state = new GameState(game.World.Name, new());
        using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<LocalTransform, Savable>().WithOptions(EntityQueryOptions.Default).Build(game.World.EntityManager);
        var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var entities = query.ToEntityArray(Allocator.Temp);
        
        for (int i = 0; i < transforms.Length; i++)
        {
            var transform = transforms[i];
            state.Entities[(transform.Position, transform.Rotation)] = new EntityState(game.World.EntityManager, entities[i]);
        }
        
        return state;
    }

    public enum DifError { None, MismatchedCount, MismatchedKeys, MismatchedValues }
    public bool Dif(GameState other, out DifError error, out string mismatch)
    {
        // Compare the two Entities dictionaries and see if they have the same keys, and the same values for each key
        if (Entities.Count != other.Entities.Count)
        {
            error = DifError.MismatchedCount;
            mismatch = $"Mismatched count: {this.Entities.Count} != {other.Entities.Count}";
            return false;
        }
        foreach (var kvp in Entities)
        {
            if (!other.Entities.TryGetValue(kvp.Key, out var otherState))
            {
                error = DifError.MismatchedKeys;
                mismatch = $"Key in '{WorldName}' not found in '{other.WorldName}': {kvp.Key}:{kvp.Value}";
                return false;
            }
            if (!kvp.Value.Equals(otherState))
            {
                error = DifError.MismatchedValues;
                mismatch = $"Mismatched values for key {kvp.Key} don't match: {kvp.Value} != {otherState}";
                return false;
            }
        }
        foreach (var kvp in other.Entities)
        {
            if (!Entities.TryGetValue(kvp.Key, out var thisState))
            {
                error = DifError.MismatchedKeys;
                mismatch = $"Key in '{other.WorldName}' not found in '{WorldName}': {kvp.Key}:{kvp.Value}";
                return false;
            }
        }
        
        error = DifError.None;
        mismatch = string.Empty;
        return true;
    }
    
    public bool Equals(GameState other)
    {
        // Compare the two Entities dictionaries and see if they have the same keys, and the same values for each key
        if (Entities.Count != other.Entities.Count)
        {
            return false;
        }
        foreach (var kvp in Entities)
        {
            if (!other.Entities.TryGetValue(kvp.Key, out var otherState) || !kvp.Value.Equals(otherState))
            {
                return false;
            }
        }
        foreach (var kvp in other.Entities)
        {
            if (!Entities.TryGetValue(kvp.Key, out var thisState))
            {
                return false;
            }
        }
        return true;
    }

    public override bool Equals(object obj)
    {
        return obj is GameState other && Equals(other);
    }

    public override int GetHashCode()
    {
        return 0; // Wah
    }

    public static bool operator ==(GameState left, GameState right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(GameState left, GameState right)
    {
        return !left.Equals(right);
    }
}