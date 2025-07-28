using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

public class ClientDesyncDebugger : MonoBehaviour
{
    [FormerlySerializedAs("TextPrefab")] public DebugTextDisplay textDisplayPrefab;
    Dictionary<PingClientBehaviour, StateData> m_ClientStates = new Dictionary<PingClientBehaviour, StateData>();
    Dictionary<Game, Dictionary<long, List<NativeArray<byte>>>> m_GameSaves = new();
    
    public bool ManualUpdates = false;
    int m_StepForward = 0;
    
    public static ClientDesyncDebugger Instance;
    public static bool Paused => Instance && Instance.ManualUpdates && Instance.m_StepForward == 0;

    private void OnEnable()
    {
        Instance = this;
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
        foreach (var state in m_ClientStates)
            state.Value.Dispose();
        foreach (var save in m_GameSaves)
        {
            foreach (var key in save.Value.Keys.ToArray())
            {
                foreach (var part in save.Value[key])
                {
                    part.Dispose();
                }
                save.Value.Remove(key);
            }
        }
    }
    
    [EditorButton]
    public void StepForward()
    {
        m_StepForward = 2;
    }

    void Update()
    {
        if (m_StepForward > 0)
            m_StepForward--;
            
        bool comparisonReady = !m_ComparisonMade && m_Comparisons.Count > 0;
        foreach (var comp in m_Comparisons)
        {
            if (!comp.IsReady)
            {
                comp.Update_NoLogic();
                comparisonReady = false;
            }
            comp.ApplyRender(0);
        }
        
        if (comparisonReady)
        {
            m_ComparisonMade = true;
            var zero = GameState.Compile(m_Comparisons[0]);
            bool match = true;
            for (int comparisonIndex = 1; comparisonIndex < m_Comparisons.Count; comparisonIndex++)
            {
                var b = GameState.Compile(m_Comparisons[1]);
                if (zero.Dif(b, out var error, out var mismatchA, out var mismatchB)) continue;
                
                if (error == GameState.DifError.MismatchedCount)
                {
                    Debug.LogError($"Mismatched count: {zero.Entities.Count} != {b.Entities.Count}");
                    match = false;
                    continue;
                }
                else if (error == GameState.DifError.MismatchedKeys)
                {
                    Debug.LogError($"Mismatched keys: {mismatchA} != {mismatchB}");
                    match = false;
                    continue;
                }
                else if (error == GameState.DifError.MismatchedValues)
                {
                    Debug.LogError($"Mismatched values for key {mismatchA} != {mismatchB}");
                    match = false;
                    continue;
                }
            }
            
            if (match)
            {
                Debug.Log($"Match!");
                CompareGameSaves(-1);
                StepForward();
            }
        }
    
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

    public void UpdateGame(long step, Game game)
    {
        Debug.Log($"Starting game loop debug for step {step} in game {game.World.Name}");
    
        // Manually update the systems in the expected order + record memory at each step
        LinkedList<SystemHandle> systems = new();
        var simSys = game.World.GetExistingSystemManaged<SimulationSystemGroup>();
        RecursiveAddSystems(game.World, simSys, in systems);
        
        StringBuilder sb = new();
        sb.AppendLine("Systems Order:");
        int i = 0;
        foreach (var system in systems)
            sb.AppendLine($"{i++}: {TypeManager.GetSystemName(game.World.Unmanaged.GetSystemTypeIndex(system))}");
        Debug.Log(sb.ToString());
        
        sb = new();
        sb.AppendLine($"Execution log:");
        
        List<NativeArray<byte>> saves = new();
        saves.Add(game.InstantSave());
        sb.AppendLine($"Init save state: {saves.Count}");
        
        foreach (var system in systems)
        {
            sb.AppendLine($"{TypeManager.GetSystemName(game.World.Unmanaged.GetSystemTypeIndex(system))}:");
            var systemType = game.World.Unmanaged.GetSystemTypeIndex(system);
            if (systemType.IsGroup)
            {
                sb.AppendLine($"\tSkipping group system...");
                continue;
            }
            
            sb.AppendLine($"\tExecuting...");
            system.Update(game.World.Unmanaged);
            
            saves.Add(game.InstantSave());
            sb.AppendLine($"\tSave state: {saves.Count}");
        }
        sb.AppendLine($"Done. Saves: {saves.Count}");
        
        if (!m_GameSaves.TryGetValue(game, out var saveHistory))
            m_GameSaves[game] = saveHistory = new();
        saveHistory[step] = saves;
        
        Debug.Log(sb.ToString());
    }

    private static void RecursiveAddSystems(World world, ComponentSystemGroup simSys, in LinkedList<SystemHandle> systems)
    {
        var internalSystems = simSys.GetAllSystems();
        foreach (var system in internalSystems)
        {
            if (world.Unmanaged.GetSystemTypeIndex(system).IsGroup)
            {
                RecursiveAddSystems(world, world.GetExistingSystemManaged(world.Unmanaged.GetSystemTypeIndex(system)) as ComponentSystemGroup, in systems);
            }
            else
            {
            
                systems.AddLast(system);
            }
        }
    }

    [EditorButton]
    public void CleanGameSaves()
    {
        // Destroy all saves before sync point
        var maxStep = m_GameSaves.Min(kvp => kvp.Value.Max(kvp => kvp.Key));
        
        foreach (var save in m_GameSaves)
        {
            foreach (var key in save.Value.Keys.ToArray())
                if (key < maxStep)
                {
                    Debug.Log($"... cleaned step {key}.");
                    foreach (var part in save.Value[key])
                        part.Dispose();
                    save.Value.Remove(key);
                }
        }
    }
    
    bool m_ComparisonMade = false;
    List<Game> m_Comparisons = new();
    [EditorButton]
    public void CompareGameSaves(int index)
    {
        m_ComparisonMade = false;
        
        foreach (var comp in m_Comparisons) comp.Dispose();
        m_Comparisons.Clear();
        
        if (m_GameSaves.Count == 0 || m_GameSaves.Any(s => s.Value.Count == 0))
        {
            m_ComparisonMade = true;
            return;
        }
    
        var minStep = m_GameSaves.Min(kvp => kvp.Value.Max(kvp => kvp.Key));
        if (m_GameSaves.Any(kvp => !kvp.Value.ContainsKey(minStep)))
        {
            m_ComparisonMade = true;
            return;
        }
        
        foreach (var save in m_GameSaves)
        {
            var tempSaveGame = new Game(true);
            m_Comparisons.Add(tempSaveGame);
            tempSaveGame.LoadSave(save.Value[minStep][index == -1 ? ^1 : index]);
            tempSaveGame.LoadScenes();
        }
    }
}


public readonly struct GameState : IEquatable<GameState>
{
    public readonly Dictionary<(float3, quaternion), EntityState> Entities;
    public GameState(Dictionary<(float3, quaternion), EntityState> entities)
    {
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
        var state = new GameState(new());
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
    public bool Dif(GameState other, out DifError mismatch, out ((float3, quaternion), EntityState) a, out ((float3, quaternion), EntityState) b)
    {
        // Compare the two Entities dictionaries and see if they have the same keys, and the same values for each key
        if (Entities.Count != other.Entities.Count)
        {
            mismatch = DifError.MismatchedCount;
            a = default;
            b = default;
            return false;
        }
        foreach (var kvp in Entities)
        {
            if (!other.Entities.TryGetValue(kvp.Key, out var otherState))
            {
                mismatch = DifError.MismatchedKeys;
                a = (kvp.Key, kvp.Value);
                b = default;
                return false;
            }
            if (!kvp.Value.Equals(otherState))
            {
                mismatch = DifError.MismatchedValues;
                a = (kvp.Key, kvp.Value);
                b = (kvp.Key, otherState);
                return false;
            }
        }
        foreach (var kvp in other.Entities)
        {
            if (!Entities.TryGetValue(kvp.Key, out var thisState))
            {
                mismatch = DifError.MismatchedKeys;
                a = default;
                b = (kvp.Key, kvp.Value);
                return false;
            }
        }
        
        mismatch = DifError.None;
        a = default;
        b = default;
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