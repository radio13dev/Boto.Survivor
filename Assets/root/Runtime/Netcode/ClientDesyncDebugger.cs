using System;
using System.Collections.Generic;
using System.Text;
using BovineLabs.Core.Extensions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class ClientDesyncDebugger : MonoBehaviour
{
    [FormerlySerializedAs("TextPrefab")] public DebugTextDisplay textDisplayPrefab;
    Dictionary<PingClientBehaviour, StateData> m_ClientStates = new Dictionary<PingClientBehaviour, StateData>();

    void Update()
    {
        // Update our client list
        long step = long.MaxValue;
        HashSet<PingClientBehaviour> toDelete = new(m_ClientStates.Keys);
        foreach (var client in FindObjectsByType<PingClientBehaviour>(FindObjectsSortMode.None))
        {
            if (client.Game == null || !client.Game.IsReady) continue;
            
            if (!m_ClientStates.TryGetValue(client, out var state))
            {
                state = m_ClientStates[client] = new StateData();
                client.Game.World.Unmanaged.GetUnsafeSystemRef<LightweightRenderSystem>(client.Game.World.Unmanaged.GetExistingUnmanagedSystem<LightweightRenderSystem>()).DebugColorOverlay = Random.ColorHSV();
            }
            step = math.min(step, client.Game.World.EntityManager.GetSingleton<StepController>().Step);
            toDelete.Remove(client);
        }
        
        // Remove clients that are no longer present
        foreach (var client in toDelete)
        {
            m_ClientStates[client].Dispose();
            m_ClientStates.Remove(client);
        }
        
        // Collect data for the current step, and clear data before our 'checking' step
        int i = 0;
        foreach (var client in m_ClientStates)
        {
            client.Value.Collect(client.Key);
            client.Value.ClearObsolete(step);
            client.Value.UpdateDisplay(textDisplayPrefab, step);
            i++;
        }
    }
    
    public class StateData : IDisposable
    {
        public List<DebugTextDisplay> SpawnedText = new();
    
        public class Point
        {
            public long Step;
            public LocalTransform[] Players = new LocalTransform[0];
        }
        
        List<Point> m_Points = new List<Point>();
    
        public void Collect(PingClientBehaviour client)
        {
            if (m_Points.Count > 0 && m_Points[^1].Step == client.Game.World.EntityManager.GetSingleton<StepController>().Step)
            {
                // Already collected for this step
                return;
            }
            
            var point = new Point
            {
                Step = client.Game.World.EntityManager.GetSingleton<StepController>().Step
            };
            
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<PlayerControlled, LocalTransform>().WithOptions(EntityQueryOptions.Default).Build(client.Game.World.EntityManager);
            var players = query.ToComponentDataArray<PlayerControlled>(Allocator.Temp);
            var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            
            point.Players = new LocalTransform[players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                point.Players[players[i].Index] = transforms[i];
            }
            m_Points.Add(point);
        }

        public void ClearObsolete(long step)
        {
            for (int i = 0; i < m_Points.Count; i++)
            {
                if (m_Points[i].Step < step)
                {
                    m_Points.RemoveAt(i);
                    i--;
                }
            }
        }

        public void UpdateDisplay(DebugTextDisplay textDisplayPrefab, long step)
        {
            if (m_Points.Count == 0 || m_Points[0].Step != step)
            {
                for (int i = 0; i < SpawnedText.Count; i++)
                {
                    SpawnedText[i].Text.text = "Missing";
                }
            }
            else
            {
                for (var index = 0; index < m_Points[0].Players.Length; index++)
                {
                    var player = m_Points[0].Players[index];
                    StringBuilder sb = new();
                    sb.AppendLine($"Player{index}: {player.Position}");

                    if (index >= SpawnedText.Count)
                    {
                        SpawnedText.Add(textDisplayPrefab.GetFromPool());
                    }
                    SpawnedText[index].transform.SetPositionAndRotation(player.Position, player.Rotation);
                    SpawnedText[index].Text.text = sb.ToString();
                }
            }
        }

        public void Dispose()
        {
            foreach (var text in SpawnedText)
            {
                text.ReturnToPool();
            }
        }
    }
}
