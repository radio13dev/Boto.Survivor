using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class MapGenMono : MonoBehaviour
{
    const int MAX_ATTEMPTS = 10;
    
    public ToroidalBlobMono Demo_ZonePrefab;
    
    [Range(10, 200)]
    public float RadiusMin = 10;
    [Range(10, 200)]
    public float RadiusMax = 30;
    [Range(1, 3)]
    public float ChildSpacing = 1.1f;
    [Range(1, 10)]
    public float NearnessSpacing = 1.1f;
    public AnimationCurve AttemptCountCurve = AnimationCurve.Linear(0, 3, 1, 6);

    [EditorButton]
    public void Demo_RespawnZones()
    {
        foreach (var zone in GetComponentsInChildren<ToroidalBlobMono>())
            DestroyImmediate(zone.gameObject);
    
        Random r = Random.CreateFromIndex((uint)DateTime.UtcNow.Ticks);
    
        List<float3> zoneSpawnPositions = new();
        List<float> zoneSpawnRadius = new();
        List<int> zoneSpawnIndices = new();
        
        bool firstPass = true;
        
        for (int loop = 0; loop < 20 && zoneSpawnPositions.Count < ToroidalBlobInit.METABALL_COUNT; loop++)
        for (int i = 0; i < ToroidalBlobInit.BLOB_COUNT; i++)
        {
            if (firstPass)
            {
                firstPass = false;
            }
            else
            {
                // Subsequent passes only have a chance to trigger
                if (r.NextFloat() < 0.5f) continue;
            }
        
            // Ensure the 'zero' isn't too close to any existing zones
            float radius = r.NextFloat(RadiusMin, RadiusMax);
            float3 zero = default;
            bool success = false;
            for (int attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
            {
                zero = TorusMapper.SnapToSurface(r.NextFloat3(-1,1)*(TorusMapper.RingRadius.Data + TorusMapper.Thickness.Data)); // 25% chance to be inside, 75% chance to be outside
                
                for (int check = 0; check < zoneSpawnPositions.Count; check++)
                {
                    if (math.distancesq(zero, zoneSpawnPositions[check]) < math.square(NearnessSpacing*(radius+zoneSpawnRadius[check])))
                    {
                        goto Retry;
                    }
                }
                
                success = true;
                break;
                
                Retry: 
                continue;
            }
            
            if (!success)
            {
                Debug.LogWarning($"Failed to spawn zone type {i} after {MAX_ATTEMPTS} attempts");
                continue;
            }
            
            int zeroIndex = zoneSpawnPositions.Count;
            int toSpawn = (int)AttemptCountCurve.Evaluate(r.NextFloat());
            for (int placement = 0; placement < toSpawn; placement++)
            {
                if (zoneSpawnPositions.Count >= ToroidalBlobInit.METABALL_COUNT) break;
                
                zoneSpawnPositions.Add(zero);
                zoneSpawnRadius.Add(radius);
                zoneSpawnIndices.Add(i);
                
                
                radius = r.NextFloat(RadiusMin,RadiusMax);
                
                success = false;
                for (int attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
                {
                    var refIndex = r.NextInt(zeroIndex, zoneSpawnPositions.Count);
                    zero = zoneSpawnPositions[refIndex];
                    var dir = math.normalizesafe(TorusMapper.ProjectOntoSurface(zero, r.NextFloat3Direction()));
                    var dist = r.NextFloat(zoneSpawnRadius[refIndex], (zoneSpawnRadius[refIndex]+radius))*ChildSpacing;
                    zero = TorusMapper.MovePointInDirection(zero, dir*dist);
                
                    for (int check = 0; check < zeroIndex; check++)
                    {
                        if (math.distancesq(zero, zoneSpawnPositions[check]) < math.square(NearnessSpacing*(radius+zoneSpawnRadius[check])))
                        {
                            goto Retry2;
                        }
                    }
                
                    success = true;
                    break;
                
                    Retry2: 
                    continue;
                }
                
                if (!success)
                {
                    Debug.LogWarning($"Failed to spawn child zone for type {i} after {MAX_ATTEMPTS} attempts");
                    continue;
                }
            }
        }
        
        for (int i = 0; i < zoneSpawnIndices.Count; i++)
        {
            var zone = Instantiate(Demo_ZonePrefab, zoneSpawnPositions[i], Quaternion.identity, transform);
            zone.Index = zoneSpawnIndices[i];
            zone.Radius = zoneSpawnRadius[i];
            zone.gameObject.SetActive(true);
        }
        
        ToroidalBlobInit.SetDirty();
    
        /*
        Game.ClientGame.CompleteDependencies();
        var prefabs = Game.ClientGame.World.EntityManager.GetSingletonBuffer<GameManager.Prefabs>(true);
        var zonePrefab = prefabs[GameManager.Prefabs.ZonePrefab];
        */
    }
}