using System;
using BovineLabs.Core.Extensions;
using BovineLabs.Saving;
using Unity.Entities;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[Save]
public struct Ring : IBufferElementData
{
    public const int k_RingCount = 8;

    public double LastActivateTime;

    public RingStats Stats;
}

[Save]
[Serializable]
public struct RingStats : IComponentData
{
    // Primary effect
    public bool IsValid => PrimaryEffect != RingPrimaryEffect.None && PrimaryEffect != RingPrimaryEffect.Length;
    public RingPrimaryEffect PrimaryEffect;

    public static RingStats Generate(ref Random random)
    {
        RingStats ringStats = new();
        ringStats.PrimaryEffect = (RingPrimaryEffect)random.NextInt(1, (int)RingPrimaryEffect.Length - 1);
        return ringStats;
    }

    public string GetTitleString()
    {
        switch (PrimaryEffect)
        {
            case RingPrimaryEffect.Projectile_Ring:
                return "Ring of Projectiles";
            case RingPrimaryEffect.Projectile_NearestRapid:
                return "Rapid Ring";
            default:
                return "???";
        }
    }

    public string GetDescriptionString()
    {
        switch (PrimaryEffect)
        {
            case RingPrimaryEffect.Projectile_Ring:
                return "Launches 8 projectiles out in a ring around you";
            case RingPrimaryEffect.Projectile_NearestRapid:
                return "Rapidly fires at the nearest target";
            default:
                return "???";
        }
    }

    #region CLIENTSIDE
    public Material Material
    {
        get
        {
            var visuals = Game.ClientGame.World.EntityManager.GetSingletonBuffer<GameManager.RingVisual>(true);
            var instances = Game.ClientGame.World.EntityManager.GetSingletonBuffer<GameManager.InstancedResources>(true);
            return instances[visuals[(int)PrimaryEffect].InstancedResourceIndex].Instance.Value.Material;
        }
    }

    public Mesh Mesh
    {
        get
        {
            var visuals = Game.ClientGame.World.EntityManager.GetSingletonBuffer<GameManager.RingVisual>(true);
            var instances = Game.ClientGame.World.EntityManager.GetSingletonBuffer<GameManager.InstancedResources>(true);
            return instances[visuals[(int)PrimaryEffect].InstancedResourceIndex].Instance.Value.Mesh;
        }
    }
    #endregion
}


public enum RingPrimaryEffect
{
    None,
    Projectile_Ring,
    Projectile_NearestRapid,

    Length
}

public enum RingSecondaryEffect
{
    Trigger_Left
}