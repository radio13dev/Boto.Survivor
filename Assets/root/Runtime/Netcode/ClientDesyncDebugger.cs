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

public class ClientDesyncDebugger : MonoBehaviour
{
    [FormerlySerializedAs("TextPrefab")] public DebugTextDisplay textDisplayPrefab;
    Dictionary<PingClientBehaviour, StateData> m_ClientStates = new Dictionary<PingClientBehaviour, StateData>();
    Dictionary<Game, Dictionary<long, List<NativeArray<byte>>>> m_GameSaves = new();
    
    public bool ManualUpdates = false;
    long m_StepForward = 0;
    
    public static ClientDesyncDebugger Instance;

    public static bool CanExecuteStep(long step)
    {
        if (!Instance) return true;
        if (!Instance.ManualUpdates) return true;
        if (Instance.m_StepForward >= step) return true;
        return false;
    }

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
        m_Comparison?.Dispose();
    }
    
    [EditorButton]
    public void StepForward()
    {
        m_StepForward++;
    }

    void Update()
    {
        if (m_Comparison?.ComparisonReady == true)
        {
            if (!m_Comparison.ComparisonMade)
            {
                if (m_Comparison.Compare())
                {
                    StepForward();
                }
            }
            else if (m_Comparison.ComparisonResult == true)
            {
                TryCompare(m_StepForward);
            }
        }
    
        // Update our client list
        long step = long.MaxValue;
        HashSet<PingClientBehaviour> toDelete = new(m_ClientStates.Keys);
        foreach (var client in FindObjectsByType<PingClientBehaviour>(FindObjectsSortMode.None))
        {
            if (client.m_Game == null || !client.m_Game.IsReady) continue;
            
            if (!m_ClientStates.TryGetValue(client, out var state))
            {
                state = m_ClientStates[client] = new StateData();
            }
            step = math.min(step, client.m_Game.World.EntityManager.GetSingleton<StepController>().Step);
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
            if (m_Points.Count > 0 && m_Points[^1].Step == client.m_Game.World.EntityManager.GetSingleton<StepController>().Step)
            {
                // Already collected for this step
                return;
            }
            
            var point = new Point
            {
                Step = client.m_Game.World.EntityManager.GetSingleton<StepController>().Step
            };
            
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<PlayerControlled, LocalTransform>().WithOptions(EntityQueryOptions.Default).Build(client.m_Game.World.EntityManager);
            using var entities = query.ToEntityArray(Allocator.Temp);
            
            point.Players = new LocalTransform[entities.Length];
            for (int i = 0; i < entities.Length; i++)
            {
                point.Players[i] = client.m_Game.World.EntityManager.GetComponentData<LocalTransform>(entities[i]);
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
        //Debug.Log($"Starting game loop debug for step {step} in game {game.World.Name}");
    
        // Manually update the systems in the expected order + record memory at each step
        LinkedList<SystemHandle> systems = new();
        var simSys = game.World.GetExistingSystemManaged<SimulationSystemGroup>();
        RecursiveAddSystems(game.World, simSys, in systems);
        
        StringBuilder sb = new();
        sb.AppendLine("Systems Order:");
        int i = 0;
        foreach (var system in systems)
            sb.AppendLine($"{i++}: {TypeManager.GetSystemName(game.World.Unmanaged.GetSystemTypeIndex(system))}");
        //Debug.Log(sb.ToString());
        
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
        
        //Debug.Log(sb.ToString());
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
                    //Debug.Log($"... cleaned step {key}.");
                    foreach (var part in save.Value[key])
                        part.Dispose();
                    save.Value.Remove(key);
                }
        }
    }
    
    GameCompare m_Comparison;
    [EditorButton]
    public void CompareGameSaves(int index)
    {
        if (m_GameSaves.Count == 0 || m_GameSaves.Any(s => s.Value.Count == 0))
        {
            return;
        }
    
        var minStep = m_GameSaves.Min(kvp => kvp.Value.Max(kvp => kvp.Key));
        if (m_GameSaves.Any(kvp => !kvp.Value.ContainsKey(minStep)))
        {
            return;
        }
        
        if (m_Comparison != null) m_Comparison.Dispose();
        m_Comparison = null;

        m_Comparison = new GameCompare(m_GameSaves, minStep, index);
    }
    
    private void TryCompare(long minStep)
    {
        if (m_GameSaves.Count == 0 || m_GameSaves.Any(s => s.Value.Count == 0))
        {
            return;
        }
    
        minStep = math.max(minStep, m_GameSaves.Min(kvp => kvp.Value.Max(kvp => kvp.Key)));
        if (m_GameSaves.Any(kvp => !kvp.Value.ContainsKey(minStep)))
        {
            return;
        }
        
        if (m_Comparison != null) m_Comparison.Dispose();
        m_Comparison = null;

        m_Comparison = new GameCompare(m_GameSaves, minStep, -1);
    }
}

public class GameCompare : IDisposable
{
    public long Step;
    public bool ComparisonReady => m_ComparisonTask == null;
    public bool ComparisonMade => ComparisonResult.HasValue;
    public bool? ComparisonResult;
    
    List<Game> m_Comparisons = new();
    Coroutine m_ComparisonTask;
    List<GameState> m_ComparisonStates = new();
    
    public GameCompare(Dictionary<Game, Dictionary<long, List<NativeArray<byte>>>> games, long step, int index)
    {
        Step = step;
        foreach (var save in games)
        {
            var i = index == -1 ? ^1 : index;
        
            var tempSaveGame = new Game(true);
            m_Comparisons.Add(tempSaveGame);
            var saveData = save.Value[step][i];
            tempSaveGame.LoadSave(saveData);
            tempSaveGame.LoadScenes();
        }
        
        m_ComparisonTask = CoroutineHost.Instance.StartCoroutine(StateLoadCo());
    }

    private IEnumerator StateLoadCo()
    {
        for (int i = 0; i < m_Comparisons.Count; i++)
        {
            while (!m_Comparisons[i].IsReady)
            {
                m_Comparisons[i].Update_NoLogic();
                yield return null;
            }
        }

        for (int i = 0; i < m_Comparisons.Count; i++)
        {
            m_ComparisonStates.Add(GameState.Compile(m_Comparisons[i]));
            yield return null;
        }
        
        m_ComparisonTask = null;
    }

    public bool Compare()
    {
        var zero = m_ComparisonStates[0];
        bool match = true;
        for (int comparisonIndex = 1; comparisonIndex < m_Comparisons.Count; comparisonIndex++)
        {
            var b = m_ComparisonStates[1];
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
            Debug.Log($"Match!");
        ComparisonResult = match;
        return match;
    }

    public void Dispose()
    {
        if (m_ComparisonTask != null && CoroutineHost.Instance) CoroutineHost.Instance.StopCoroutine(m_ComparisonTask);
        for (int i = 0; i < m_Comparisons.Count; i++) m_Comparisons[i].Dispose();
    }

    public void Update_Render()
    {
        for (int i = 0; i < m_Comparisons.Count; i++)
            m_Comparisons[i].ApplyRender(0);
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