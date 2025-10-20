using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BovineLabs.Core.Extensions;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Transforms;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = Unity.Mathematics.Random;

public class LobbySpawnerSingleplayer : MonoBehaviour
{
    public const float LobbyRefreshTime = 4;
    
    public static Dictionary<string, (float3, Entity)> m_ClientsideLobbyReferences=new();
    Stopwatch sw;
    ExclusiveCoroutine co;

    private void Start()
    {
        if (!GameLaunch.IsSignedIn)
        {
            GameLaunch.SignIn();
            return;
        }
    }

    private void Update()
    {
        if (!GameLaunch.IsSignedIn)
            return;
            
        if (sw == null) sw = Stopwatch.StartNew();
        
        if (sw.Elapsed.Seconds > LobbyRefreshTime)
        {
            sw.Restart();
            
            co.StartCoroutine(this, RefreshLobbies());
        }
    }
    
    IEnumerator RefreshLobbies()
    {
        Debug.Log($"Scanning for lobbies...");
        
        var task = LobbyService.Instance.QueryLobbiesAsync();
        while (!task.IsCompleted) yield return null;
        
        if (task.IsFaulted)
            throw task.Exception;
            
        if (!GameLaunch.Main || !GameLaunch.Main.IsSingleplayer)
        {
            Debug.Log($"Game not launched or not in singleplayer, canceled.");
            yield break;
        }
        
        
        var game = GameLaunch.Main.Singleplayer.Game;
        var resources = game.World.EntityManager.GetSingletonBuffer<GameManager.Prefabs>();
        
        Random r = Random.CreateFromIndex(0);
        List<float3> existingLobbies = null;
        
        var toDestroy = new HashSet<string>(m_ClientsideLobbyReferences.Keys);
        var lobbies = task.Result;
        
        var mthd = typeof(Lobby).GetMethod("SerializeAsPathParam", BindingFlags.Instance | BindingFlags.NonPublic);
        Debug.Log("Lobbies found:\n- " + string.Join("\n- ", lobbies.Results.Select(l => mthd.Invoke(l, Array.Empty<object>()))));
        
        for (int i = 0; i < lobbies.Results.Count; i++)
        {
            toDestroy.Remove(lobbies.Results[i].Id);
            
            if (m_ClientsideLobbyReferences.ContainsKey(lobbies.Results[i].Id)) continue;
            
            if (existingLobbies == null)
                existingLobbies = new List<float3>(m_ClientsideLobbyReferences.Values.Select(l => l.Item1));
            
            var target = CameraTarget.MainTarget.transform;
            float2 dir = r.NextFloat2Direction();
            float3 shift = target.forward*dir.x + target.right*dir.y;
            float3 spawnPos = TorusMapper.SnapToSurface((float3)target.position + shift);
            for (int j = 0; j < existingLobbies.Count; j++)
            {
                if (math.distancesq(existingLobbies[j], spawnPos) < LobbyZone.ColliderRadius*LobbyZone.ColliderRadius)
                {
                    // Move away from player
                    shift = TorusMapper.ProjectOntoSurface(spawnPos, shift);
                    shift = math.normalize(shift);
                    spawnPos = TorusMapper.MovePointInDirection(spawnPos, shift*LobbyZone.ColliderRadius*3f);
                    
                    // Reset
                    j = -1;
                }
            }
            
            game.CompleteDependencies();
            var lobby = game.World.EntityManager.Instantiate(resources[GameManager.Prefabs.LobbyTemplate].Entity);
            game.World.EntityManager.SetComponentData(lobby, LocalTransform.FromPosition(spawnPos));
            game.World.EntityManager.SetComponentData(lobby, LobbyZone.ClientsideLobby);
            m_ClientsideLobbyReferences.Add(lobbies.Results[i].Id, (spawnPos, lobby));
            existingLobbies.Add(spawnPos);
        }
        
        foreach (var destroy in toDestroy)
        {
            if (m_ClientsideLobbyReferences.Remove(destroy, out var lobby))
                game.World.EntityManager.DestroyEntity(lobby.Item2);
        }
        
        // Also scan joined lobbies
        var task2 = LobbyService.Instance.GetJoinedLobbiesAsync();
        while (!task2.IsCompleted) yield return null;
        
        if (task2.IsFaulted)
            throw task2.Exception;
            
        Debug.Log("Lobbies already in:\n- " + string.Join("\n- ", task2.Result));
    }
}