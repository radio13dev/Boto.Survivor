using System;
using System.Collections.Generic;
using System.Linq;
using AYellowpaper.SerializedCollections;
using Drawing;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class MapGenMono : MonoBehaviourGizmos
{
    const int MAX_ATTEMPTS = 10;

    public ToroidalBlobMono Demo_ZonePrefab;

    [Range(10, 200)] public float RadiusMin = 10;
    [Range(10, 200)] public float RadiusMax = 30;
    [Range(1, 3)] public float ChildSpacing = 1.1f;
    [Range(1, 10)] public float NearnessSpacing = 1.1f;
    [Range(0, 2)] public float EventCenterNearness = 0.7f;
    [Range(0, 3)] public float EventSpread = 1f;

    public AnimationCurve AttemptCountCurve = AnimationCurve.Linear(0, 3, 1, 6);

    [EditorButton]
    public void Demo_RespawnZones()
    {
        foreach (var zone in GetComponentsInChildren<ToroidalBlobMono>())
            DestroyImmediate(zone.gameObject);

        Random r = Random.CreateFromIndex((uint)DateTime.UtcNow.Ticks);

        List<float3> zoneSpawnPositions = new();
        List<float> zoneSpawnRadius = new();
        List<byte> zoneSpawnIndices = new();

        bool firstPass = true;

        for (int loop = 0; loop < 40 && (zoneSpawnPositions.Count < ToroidalBlobInit.METABALL_COUNT || zoneSpawnPositions.Count < 6); loop++)
        for (byte i = 0; i < ToroidalBlobInit.BLOB_COUNT; i++)
        {
            if (r.NextFloat() > 1.0f / ToroidalBlobInit.BLOB_COUNT) continue;

            // Ensure the 'zero' isn't too close to any existing zones
            float radius = r.NextFloat(RadiusMin, RadiusMax);
            float3 zero = default;
            bool success = false;
            for (int attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
            {
                zero = TorusMapper.SnapToSurface(r.NextFloat3(-1, 1) *
                                                 (TorusMapper.RingRadius.Data + TorusMapper.Thickness.Data)); // 25% chance to be inside, 75% chance to be outside

                for (int check = 0; check < zoneSpawnPositions.Count; check++)
                {
                    if (math.distancesq(zero, zoneSpawnPositions[check]) < math.square(NearnessSpacing * (radius + zoneSpawnRadius[check])))
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


                radius = r.NextFloat(RadiusMin, RadiusMax);

                success = false;
                for (int attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
                {
                    var refIndex = r.NextInt(zeroIndex, zoneSpawnPositions.Count);
                    zero = zoneSpawnPositions[refIndex];
                    var dir = math.normalizesafe(TorusMapper.ProjectOntoSurface(zero, r.NextFloat3Direction()));
                    var dist = r.NextFloat(zoneSpawnRadius[refIndex], (zoneSpawnRadius[refIndex] + radius)) * ChildSpacing;
                    zero = TorusMapper.MovePointInDirection(zero, dir * dist);

                    for (int check = 0; check < zeroIndex; check++)
                    {
                        if (math.distancesq(zero, zoneSpawnPositions[check]) < math.square(NearnessSpacing * (radius + zoneSpawnRadius[check])))
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

            // For each spawned blob create their event
            float3 eventPos = zoneSpawnPositions[i];
            bool success = false;
            for (int attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
            {
                var dir = math.normalizesafe(TorusMapper.ProjectOntoSurface(zone.transform.position, r.NextFloat3Direction()));
                var dist = r.NextFloat(zone.Radius * EventCenterNearness);
                eventPos = TorusMapper.MovePointInDirection(zone.transform.position, dir * dist);

                for (int check = 0; check < i; check++)
                {
                    if (math.distancesq(eventPos, zoneSpawnPositions[check]) < math.square(zoneSpawnRadius[check] + zone.Radius) * EventSpread)
                    {
                        success = false;
                        break;
                    }

                    success = true;
                }

                if (success) break;
            }

            if (success)
            {
                // Spawn it!
            }
        }

        ToroidalBlobInit.SetDirty();

        /*
        Game.ClientGame.CompleteDependencies();
        var prefabs = Game.ClientGame.World.EntityManager.GetSingletonBuffer<GameManager.Prefabs>(true);
        var zonePrefab = prefabs[GameManager.Prefabs.ZonePrefab];
        */
    }

    [Range(0.5f, 10f)]
    public float WallResolution = 1f;
    public int MaxWallAttempts = 1000;
    public int TestIndex = -1;
    
    [EditorButton]
    public void Demo_GenerateWalls2()
    {
        var blobs = GetComponentsInChildren<ToroidalBlobMono>();
        using var blobsNative = new NativeArray<Metaball>(blobs.Select(b => b.GetMetaball()).ToArray(), Allocator.Temp);
        int blobIndex = -1;
        foreach (var source in blobs)
        {
            source.m_Walls.Clear();
            source.m_WallsActual.Clear();
            
            blobIndex++;
            if (TestIndex != -1 && blobIndex != TestIndex) continue;
            
            float3 pos = source.transform.position;
            float3 zero = pos;
            if (MapGenSystem.GetBlobAtPoint(pos, blobsNative) != source.Index)
            {
                Debug.LogWarning($"Blob invalid! Its metaball type isn't underneath it.");
                continue;
            }

            List<(float3, byte)> path = new();
            List<Vector3> actualPath = new();
            path.Add((pos, 0)); // Add start pos

            // Path towards blob with different type
            float3 target = pos;
            for (int i = 0; i < blobs.Length; i++)
            {
                if (blobs[i].Index != source.Index)
                {
                    target = blobs[i].transform.position;
                    break;
                }
            }

            // Each step check if we're inside our index type
            float3 oldPos = pos;
            for (int i = 0; i < MaxWallAttempts; i++)
            {
                oldPos = pos;
                float3 dir = TorusMapper.GetDirection(pos, target);
                pos = TorusMapper.MovePointInDirection(pos, dir*WallResolution, 1);
                if (MapGenSystem.GetBlobAtPoint(pos, blobsNative) != source.Index)
                {
                    path.Add((pos, 2));
                    break;
                }
                else
                {
                    path.Add((pos, 1));
                }
            }

            pos = (pos + oldPos) / 2;
            path.Add((pos, 3));
            actualPath.Add(pos);
            
            // Just make the old pos an 'estimate' for how the wall should curve
            oldPos = TorusMapper.MovePointInDirection(pos, -math.cross(math.normalize(TorusMapper.GetDirection(pos, zero)), TorusMapper.GetNormal(pos)));
            
            byte lastIndex = MapGenSystem.GetBlobAtPoint(pos, blobsNative);

            // Step forward, perpendicular to the center of our blob, and move in/out until we have opposite polarity to the last point
            bool hasMovedAwayFromPathStart = false;
            float3 pathStart = pos;
            for (int pathingStep = 0; pathingStep < MaxWallAttempts; pathingStep++)
            {
                var normal = TorusMapper.GetNormal(pos);
                var forward = math.normalizesafe(TorusMapper.ProjectOntoSurface(pos, pos - oldPos));
                var toCenter = math.cross(normal, forward)*WallResolution; 
                
                //var toCenter = TorusMapper.GetDirection(pos, zero);
                //var forward = math.cross(math.normalize(toCenter), normal);
                
                oldPos = pos;
                pos = TorusMapper.SnapToSurface(pos + forward*WallResolution);

                for (int indexRepairStep = 0; indexRepairStep < 10; indexRepairStep++)
                {
                    byte index = MapGenSystem.GetBlobAtPoint(pos, blobsNative);
                    if ((index == source.Index) != (lastIndex == source.Index))
                    {
                        lastIndex = index;
                        break;
                    }

                    path.Add((pos, 6));
                    pos = TorusMapper.MovePointInDirection(pos, (index == source.Index ? -toCenter : toCenter), 1); // If we're inside we'll want to move outside, vice versa
                    pos = TorusMapper.MovePointInDirection(pos, -forward*0.4f*indexRepairStep, 1); // If we're inside we'll want to move outside, vice versa
                }

                if (!hasMovedAwayFromPathStart)
                {
                    if (math.distancesq(pathStart, pos) > WallResolution*WallResolution*2)
                        hasMovedAwayFromPathStart = true;
                }
                else if (math.distancesq(pathStart, pos) < WallResolution*WallResolution*1.5f)
                {
                    path.Add((pos, 4));
                    actualPath.Add(pos);
                    break;
                }
                
                //pos = (pos + oldPos)/2;
                path.Add((pos, 5)); // Found point
                actualPath.Add(pos);
            }
                
            // Check if the first point of the actual path is near another blobs path, if it is, use that path.
            bool shouldClean = true;
            foreach (var other in blobs)
            {
                if (other.Index != source.Index) continue;
                
                foreach (var otherWallPoint in other.m_WallsActual)
                {
                    if (math.distancesq(actualPath[0], otherWallPoint) < WallResolution*WallResolution*1.5f)
                    {
                        path = other.m_Walls;
                        actualPath = other.m_WallsActual;
                        shouldClean = false;
                        break;
                    }
                }
                if (!shouldClean) break;
            }

            // Clean the path up, average every pair of points
            if (shouldClean && actualPath.Count >= 4)
            {
                List<Vector3> cleanedPath = new();
                for (int i = 0; i < actualPath.Count - 1; i++)
                {
                    cleanedPath.Add((actualPath[i] + actualPath[i+1])/2);
                }
                actualPath = cleanedPath;
            }
            
            source.m_Walls = path;
            source.m_WallsActual = actualPath;
        }
    }
    
    public Transform WallMeshContainer;
    public MapGenWallMesh[] WallMeshTemplates;
    public SerializedDictionary<int, Material[]> WallMeshMaterials = new();
    
    [Range(0,2f)] 
    public float WallRandDistance = 0f;
    [Range(-10,10)]
    public float FloorOffsetMin = -2f;
    [Range(-10,10)]
    public float FloorOffsetMax = -4f;
    
    [EditorButton]
    public void Demo_PlaceWallMeshes()
    {
        var toDestroy = WallMeshContainer.GetComponentsInChildren<MapGenWallMesh>();
        foreach (var wallMesh in toDestroy)
            DestroyImmediate(wallMesh.gameObject);
        
        int blobIndex = -1;
        var blobs = GetComponentsInChildren<ToroidalBlobMono>();
        List<(float3 position, int meshIndex, int blobIndex)> spawned = new();
        foreach (var blob in blobs)
        {
            blobIndex++;
            Random r = Random.CreateFromIndex((uint)blobIndex);

            for (var wallIndex = 0; wallIndex < blob.m_WallsActual.Count; wallIndex++)
            {
                // Attempt 2 times per wall index: At t=0.0, then at t=0.5
                var point = blob.m_WallsActual[wallIndex];
                TrySpawnWall(point);
                TrySpawnWall((point + blob.m_WallsActual[(wallIndex+1) % blob.m_WallsActual.Count])/2);
                
                void TrySpawnWall(Vector3 point)
                {
                    // Choose a random mesh to spawn
                    var meshIndex = r.NextInt(WallMeshTemplates.Length);
                    var mesh = WallMeshTemplates[meshIndex];
                    var pos = (float3)point;
                    pos += r.NextFloat3Direction() * mesh.Radius * WallRandDistance;
                    pos += TorusMapper.GetNormal(pos) * r.NextFloat(FloorOffsetMin, FloorOffsetMax);

                    // Check if we can spawn here
                    bool canSpawn = true;
                    foreach (var other in spawned)
                    {
                        if (math.distancesq(pos, other.position) < math.square(mesh.Radius + WallMeshTemplates[other.meshIndex].Radius))
                        {
                            canSpawn = false;
                            break;
                        }
                    }

                    if (!canSpawn) return;


                    // Spawn wall mesh
                    spawned.Add((pos, meshIndex, blobIndex));
                }
            }
        }
        
        Random finalR = Random.CreateFromIndex((uint)spawned.Count);
        foreach (var toSpawn in spawned)
        {
            var rot = finalR.NextQuaternionRotation();
            if (TestIndex != -1 && toSpawn.blobIndex != TestIndex) continue;
            var mesh = Instantiate(WallMeshTemplates[toSpawn.meshIndex], toSpawn.position, rot, WallMeshContainer);
            var materials = WallMeshMaterials[blobs[toSpawn.blobIndex].Index];
            mesh.MeshRenderer.material = materials[toSpawn.meshIndex % materials.Length];
        }
    }
}

public struct Metaball : IComponentData
{
    public float3 Position;
    public float RadiusSqr;
    public byte Index;
}

[UpdateInGroup(typeof(SurvivorWorldInitSystemGroup))]
public partial struct MapGenSystem : ISystem
{
    public unsafe struct BlobInfluence
    {
        public fixed float Values[ToroidalBlobInit.BLOB_COUNT];
        public int Count => ToroidalBlobInit.BLOB_COUNT;

        public float this[int i]
        {
            get => Values[i];
            set => Values[i] = value;
        }
    }

    public static byte GetBlobAtPoint(float3 p, in NativeArray<Metaball> metaballs)
    {
        float3 checker = p;

        float threshold = 0.0f;

        var influences = new BlobInfluence();
        for (int i = 0; i < influences.Count; i++) influences[i] = 0;

        for (int i = 0; i < metaballs.Length; i++)
        {
            var ball = metaballs[i];
            if (ball.Index >= influences.Count) continue;

            float3 mbPos = ball.Position;
            float mbRadSqr = ball.RadiusSqr;

            float currInfl = mbRadSqr;
            currInfl /= (math.square(checker.x - mbPos.x) + math.square(checker.y - mbPos.y) + math.square(checker.z - mbPos.z));
            influences[ball.Index] += currInfl;
        }

        // Find the thing with the highest influence
        byte best = 0;
        float infl = 0;
        for (byte i = 0; i < influences.Count; i++)
        {
            float blobInfluence = influences[i];
            if (blobInfluence > infl)
            {
                best = i;
                infl = blobInfluence;
            }
        }

        byte hitIndex = 0;
        if (infl > threshold)
            hitIndex = best;
        return hitIndex;
    }
}