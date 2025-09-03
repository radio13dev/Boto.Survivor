using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
[StructLayout(LayoutKind.Explicit, Size = 16, Pack = 2)]
public unsafe struct GameRpc : IComponentData
{
    public enum Code
    {
        // Server Actions
        _ServerActionBit = 0b1000_0000,
        PlayerLeave = 0b1000_0001,

        // Runtime Actions
        
        PlayerSlotInventoryGemIntoRing = 0b0000_0001,
        PlayerSwapGemSlots = 0b0000_0010,
        PlayerSwapRingSlots = 0b0000_0011,
        PlayerPickupRing = 0b0000_0100,
        PlayerDropRing = 0b0000_0101,
        PlayerJoin = 0b0000_0110,
        PlayerLevelStat = 0b0000_0111,

        
        // Admin Actions
        AdminPlaceEnemy = 0b0100_0000, // Admin action flag
        AdminPlaceGem = 0b0100_0001,
        AdminPlayerLevelStat = 0b0100_0010,
    }

    public const int Length = sizeof(long)*2;
    [SerializeField] [FieldOffset(0)] long m_WRITE0;
    [SerializeField] [FieldOffset(8)] long m_WRITE1;

    [FieldOffset(0)] byte m_Type;
    [FieldOffset(1)] byte m_Player;
    [FieldOffset(0)] fixed byte m_Data[Length];
    public bool IsValidClientRpc => (m_Type & (byte)Code._ServerActionBit) == 0;

    public Code Type
    {
        get => (Code)m_Type;
        set => m_Type = (byte)value;
    }
    public byte PlayerId
    {
        get => m_Player;
        set => m_Player = value;
    }

    public override string ToString()
    {
        StringBuilder sb = new();
        sb.Append($"{Type}:{PlayerId}:");
        switch (Type)
        {
            case Code.PlayerJoin:
                break;
            case Code.PlayerLeave:
                break;
            case Code.PlayerSwapGemSlots:
            case Code.PlayerSwapRingSlots:
                sb.Append(FromSlotIndex + ":" + ToSlotIndex);
                break;
            case Code.PlayerSlotInventoryGemIntoRing:
                sb.Append(InventoryIndex + ":" + ToSlotIndex);
                break;
            default:
                sb.Append($"Missing.ToString()");
                break;
        }
        return sb.ToString();
    }

    public void Write(ref DataStreamWriter writer)
    {
        writer.WriteLong(m_WRITE0);
        writer.WriteLong(m_WRITE1);
    }

    public static GameRpc Read(ref DataStreamReader reader)
    {
        return new GameRpc(){ m_WRITE0 = reader.ReadLong(), m_WRITE1 = reader.ReadLong() };
    }

    public void Apply(Game game)
    {
        var rpc = game.World.EntityManager.CreateEntity(new ComponentType(typeof(GameRpc)));
        game.World.EntityManager.SetComponentData(rpc, this);
    }
    
    #region PlayerJoin and PlayerLeave
    public static GameRpc PlayerJoin(byte playerId, byte characterType)
    {
        return new GameRpc(){ Type = GameRpc.Code.PlayerJoin, PlayerId = playerId, SpawnType = characterType };
    }
    #endregion

    #region GemSlotting and RingSlotting
    [FieldOffset(2)] public byte InventoryIndex;
    [FieldOffset(2)] public byte FromSlotIndex;
    [FieldOffset(3)] public byte ToSlotIndex;
    [FieldOffset(4)] public float3 InteractPosition;

    public static GameRpc PlayerSlotInventoryGemIntoRing(byte player, byte inventoryIndex, byte toSlotIndex)
    {
        return new GameRpc(){ m_Type = (byte)Code.PlayerSlotInventoryGemIntoRing, m_Player = player, InventoryIndex = inventoryIndex, ToSlotIndex = toSlotIndex };
    }

    public static GameRpc PlayerSwapGemSlots(byte player, byte fromSlotIndex, byte toSlotIndex)
    {
        return new GameRpc(){ m_Type = (byte)Code.PlayerSwapGemSlots, m_Player = player, FromSlotIndex = fromSlotIndex, ToSlotIndex = toSlotIndex };
    }

    public static GameRpc PlayerUnslotGem(byte player, byte fromSlotIndex)
    {
        return new GameRpc(){ m_Type = (byte)Code.PlayerSwapGemSlots, m_Player = player, FromSlotIndex = fromSlotIndex, ToSlotIndex = byte.MaxValue };
    }
    
    public static GameRpc PlayerSwapRingSlots(byte player, byte fromSlotIndex, byte toSlotIndex)
    {
        return new GameRpc() { Type = Code.PlayerSwapRingSlots, PlayerId = player, FromSlotIndex = fromSlotIndex, ToSlotIndex = toSlotIndex };
    }
    public static GameRpc PlayerPickupRing(byte player, byte toSlotIndex, float3 interactPosition)
    {
        return new GameRpc() { Type = Code.PlayerPickupRing, PlayerId = player, FromSlotIndex = byte.MaxValue, ToSlotIndex = toSlotIndex, InteractPosition = interactPosition};
    }
    public static GameRpc PlayerDropRing(byte player, byte dropSlotIndex)
    {
        return new GameRpc() { Type = Code.PlayerDropRing, PlayerId = player, ToSlotIndex = dropSlotIndex};
    }
    #endregion
    
    #region Stats and Leveling
    public TiledStat AffectedStat => (TiledStat)m_AffectedStat;
    [FieldOffset(2)] public byte m_AffectedStat;
    [FieldOffset(3)] public bool ShouldLowerStat;
    public static GameRpc PlayerLevelStat(byte player, TiledStat tiledStat)
    {
        return new GameRpc() { Type = Code.PlayerLevelStat, PlayerId = player, m_AffectedStat = (byte)tiledStat};
    }
    public static GameRpc AdminPlayerLevelStat(byte player, TiledStat tiledStat, bool shouldRaise)
    {
        return new GameRpc() { Type = Code.AdminPlayerLevelStat, PlayerId = player, m_AffectedStat = (byte)tiledStat, ShouldLowerStat = !shouldRaise};
    }
    #endregion

    #region Admin
    [Flags]
    public enum EnemySpawnOptions
    {
        None,
        InfiniteHealth = 1 << 0,
        NoAi = 1 << 1,
        Stationary = 1 << 2
    }
    
    [FieldOffset(2)] public byte SpawnType;
    [FieldOffset(3)] public byte SpawnOptions;
    [FieldOffset(4)] public float3 PlacePosition;
    
    public static GameRpc AdminPlaceEnemy(byte player, Vector3 placePosition, byte spawnType, EnemySpawnOptions spawnOptions)
    {
        return new GameRpc() { Type = Code.AdminPlaceEnemy, PlayerId = player, SpawnType = spawnType, PlacePosition = placePosition, SpawnOptions = (byte)spawnOptions };
    }

    public static GameRpc AdminPlaceGem(byte player, Vector3 placePosition)
    {
        return new GameRpc() { Type = Code.AdminPlaceGem, PlayerId = player, PlacePosition = placePosition };
    }
    #endregion
}

/// <summary>
/// RPCs are executed in the initialization system group because their data can't be saved.
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
[UpdateBefore(typeof(PlayerControlledSystem))]
public partial struct GameRpcSystem : ISystem
{
    EntityQuery m_RpcQuery;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<StepController>();
        state.RequireForUpdate<SharedRandom>();
        state.RequireForUpdate<GameManager.Resources>();
        m_RpcQuery = SystemAPI.QueryBuilder().WithAll<GameRpc>().Build();
        state.RequireForUpdate(m_RpcQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp, PlaybackPolicy.SinglePlayback);
        using var rpcs = m_RpcQuery.ToComponentDataArray<GameRpc>(Allocator.Temp);
        using var rpcsE = m_RpcQuery.ToEntityArray(Allocator.Temp);
        
        for (int rpcIt = 0; rpcIt < rpcs.Length; rpcIt++)
        {
            var rpc = rpcs[rpcIt];
            var rpcE = rpcsE[rpcIt];
            
            Debug.Log($"{state.WorldUnmanaged.Name} Step {SystemAPI.GetSingleton<StepController>().Step}: Executing rpc: {rpc}");
            state.EntityManager.DestroyEntity(rpcE);

            var playerId = rpc.PlayerId;
            var playerTag = new PlayerControlled() { Index = playerId };
            switch (rpc.Type)
            {
                default:
                    Debug.LogError($"Unhandled RPC type: {rpc.Type}");
                    break;
                
                case GameRpc.Code.PlayerJoin:
                {
                    using var playerQuery = state.EntityManager.CreateEntityQuery(typeof(PlayerControlled));
                    playerQuery.SetSharedComponentFilter(playerTag);
                    if (playerQuery.CalculateEntityCount() > 0)
                    {
                        Debug.Log($"Player {playerId} already exists, not creating a new one...");
                        continue;
                    }
                    
                    var survivors = SystemAPI.GetSingletonBuffer<GameManager.Survivors>();
                    if (rpc.SpawnType < 0 || rpc.SpawnType >= survivors.Length)
                    {
                        Debug.LogWarning($"{rpc.SpawnType} is not a valid character type, defaulting to 0.");
                        rpc.SpawnType = 0;
                    }
                    
                    using var playerTransformsQ = state.EntityManager.CreateEntityQuery(typeof(SurvivorTag), typeof(LocalTransform));
                    using var playerTransforms = playerTransformsQ.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                    float3 avgPos = new float3(1,0,0);
                    if (playerTransforms.Length > 0)
                    {
                        for (int i = 0; i < playerTransforms.Length; i++)
                        {
                            avgPos += playerTransforms[i].Position;
                        }
                        avgPos /= playerTransforms.Length;
                    }
                    
                    using var spawnPointQ = state.EntityManager.CreateEntityQuery(typeof(SurvivorSpawnPoint), typeof(LocalTransform));
                    using var spawnPoints = spawnPointQ.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                    float3 closest = avgPos;
                    float closestD = float.MaxValue;
                    if (spawnPoints.Length == 0)
                    {
                        Debug.LogError($"Couldn't find spawn point.");
                    }
                    for (int i = 0; i < spawnPoints.Length; i++)
                    {
                        var d = math.distancesq(spawnPoints[i].Position, avgPos);
                        if (d < closestD)
                        {
                            closest = spawnPoints[i].Position;
                            closestD = d;
                        }
                    }

                    var newPlayer = ecb.Instantiate(survivors[rpc.SpawnType].Entity);
                    ecb.SetComponent(newPlayer, new PlayerControlledSaveable(){ Index = playerTag.Index });
                    ecb.SetComponent(newPlayer, LocalTransform.FromPosition(closest));
                    Debug.Log($"Player {playerId} created.");
                    break;
                }
                case GameRpc.Code.PlayerLeave:
                {
                    using var playerQuery = state.EntityManager.CreateEntityQuery(typeof(PlayerControlled));
                    playerQuery.SetSharedComponentFilter(playerTag);
                    ecb.DestroyEntity(playerQuery, EntityQueryCaptureMode.AtPlayback);
                    Debug.Log($"Player {playerId} removed.");
                    break;
                }
                
                case GameRpc.Code.PlayerSlotInventoryGemIntoRing:
                {
                    using var playerQuery = state.EntityManager.CreateEntityQuery(typeof(PlayerControlled), typeof(InventoryGem), typeof(EquippedGem), typeof(LocalTransform));
                    playerQuery.SetSharedComponentFilter(playerTag);
                    if (!playerQuery.HasSingleton<InventoryGem>()) continue;
                    
                    var playerE = playerQuery.GetSingletonEntity();
                    var inventory = SystemAPI.GetBuffer<InventoryGem>(playerE);
                    var equipped = SystemAPI.GetBuffer<EquippedGem>(playerE);
                    var rings = SystemAPI.GetBuffer<Ring>(playerE);
                    
                    if (rpc.InventoryIndex < 0 || rpc.InventoryIndex >= inventory.Length)
                    {
                        Debug.LogWarning($"Player {playerId} attempted to use inventory item at index {rpc.InventoryIndex} but inventory only has {inventory.Length} items.");
                        continue;
                    }
                    
                    if (rpc.ToSlotIndex < 0 || rpc.ToSlotIndex >= equipped.Length)
                    {
                        Debug.LogWarning($"Player {playerId} attempted to slot into index {rpc.ToSlotIndex} but only has {equipped.Length} slots.");
                        continue;
                    }
                    
                    if (equipped[rpc.ToSlotIndex].Gem.IsValid)
                        inventory.Add(new InventoryGem(equipped[rpc.ToSlotIndex].Gem));
                    equipped[rpc.ToSlotIndex] = new EquippedGem(inventory[rpc.InventoryIndex].Gem);
                    inventory.RemoveAtSwapBack(rpc.InventoryIndex);
                    
                    var dirty = SystemAPI.GetComponent<CompiledStatsDirty>(playerE);
                    dirty.SetDirty(rings[rpc.ToSlotIndex/Gem.k_GemsPerRing]);
                    SystemAPI.SetComponent(playerE, dirty);
                    
                    SystemAPI.SetComponentEnabled<CompiledStatsDirty>(playerE, true);
                    break;
                }
                case GameRpc.Code.PlayerSwapGemSlots:
                {
                    using var playerQuery = state.EntityManager.CreateEntityQuery(typeof(PlayerControlled), typeof(InventoryGem), typeof(EquippedGem), typeof(LocalTransform));
                    playerQuery.SetSharedComponentFilter(playerTag);
                    if (!playerQuery.HasSingleton<InventoryGem>()) continue;
                    
                    var playerE = playerQuery.GetSingletonEntity();
                    var equipped = SystemAPI.GetBuffer<EquippedGem>(playerE);
                    var rings = SystemAPI.GetBuffer<Ring>(playerE);
                    
                    if (rpc.FromSlotIndex < 0 || rpc.FromSlotIndex >= equipped.Length)
                    {
                        Debug.LogWarning($"Player {playerId} attempted to slot from index {rpc.FromSlotIndex} but only has {equipped.Length} slots.");
                        continue;
                    }
                    
                    if (rpc.ToSlotIndex == -1)
                    {
                        // We're moving this item to the inventory.
                        var inventory = SystemAPI.GetBuffer<InventoryGem>(playerE);
                        inventory.Add(new InventoryGem(equipped[rpc.FromSlotIndex].Gem));
                        equipped[rpc.FromSlotIndex] = default;
                        
                        var dirty = SystemAPI.GetComponent<CompiledStatsDirty>(playerE);
                        dirty.SetDirty(rings[rpc.FromSlotIndex/Gem.k_GemsPerRing]);
                        SystemAPI.SetComponent(playerE, dirty);
                    }
                    else
                    {
                        if (rpc.ToSlotIndex < 0 || rpc.ToSlotIndex >= equipped.Length)
                        {
                            Debug.LogWarning($"Player {playerId} attempted to slot into index {rpc.ToSlotIndex} but only has {equipped.Length} slots.");
                            continue;
                        }
                    
                        (equipped[rpc.ToSlotIndex], equipped[rpc.FromSlotIndex]) = (equipped[rpc.FromSlotIndex], equipped[rpc.ToSlotIndex]);
                        
                        var dirty = SystemAPI.GetComponent<CompiledStatsDirty>(playerE);
                        dirty.SetDirty(rings[rpc.FromSlotIndex/Gem.k_GemsPerRing]);
                        dirty.SetDirty(rings[rpc.ToSlotIndex/Gem.k_GemsPerRing]);
                        SystemAPI.SetComponent(playerE, dirty);
                    }
                    
                    SystemAPI.SetComponentEnabled<CompiledStatsDirty>(playerE, true);
                    break;
                }
                case GameRpc.Code.PlayerSwapRingSlots:
                {
                    using var playerQuery = state.EntityManager.CreateEntityQuery(typeof(PlayerControlled), typeof(Ring), typeof(EquippedGem), typeof(LocalTransform));
                    playerQuery.SetSharedComponentFilter(playerTag);
                    if (!playerQuery.HasSingleton<Ring>()) continue;
                    
                    var playerE = playerQuery.GetSingletonEntity();
                    var rings = SystemAPI.GetBuffer<Ring>(playerE);
                    
                    if (rpc.FromSlotIndex < 0 || rpc.FromSlotIndex >= rings.Length)
                    {
                        Debug.LogWarning($"Player {playerId} attempted to slot from index {rpc.FromSlotIndex} but only has {rings.Length} slots.");
                        continue;
                    }
                    
                    if (rpc.ToSlotIndex == -1)
                    {
                        var dirty = SystemAPI.GetComponent<CompiledStatsDirty>(playerE);
                        dirty.SetDirty(rings[rpc.FromSlotIndex]);
                        SystemAPI.SetComponent(playerE, dirty);
                        
                        // Drop this on the ground
                        rings[rpc.FromSlotIndex] = default;
                    }
                    else
                    {
                        if (rpc.ToSlotIndex < 0 || rpc.ToSlotIndex >= rings.Length)
                        {
                            Debug.LogWarning($"Player {playerId} attempted to slot into index {rpc.ToSlotIndex} but only has {rings.Length} slots.");
                            continue;
                        }
                    
                        (rings[rpc.ToSlotIndex], rings[rpc.FromSlotIndex]) = (rings[rpc.FromSlotIndex], rings[rpc.ToSlotIndex]);
                        
                        var dirty = SystemAPI.GetComponent<CompiledStatsDirty>(playerE);
                        dirty.SetDirty(rings[rpc.ToSlotIndex]);
                        dirty.SetDirty(rings[rpc.FromSlotIndex]);
                        SystemAPI.SetComponent(playerE, dirty);
                            
                        // Also swap the equipped gems
                        var equipped = SystemAPI.GetBuffer<EquippedGem>(playerE);
                        for (int i = 0; i < Gem.k_GemsPerRing; i++)
                        {
                            var fromIndex = rpc.FromSlotIndex * Gem.k_GemsPerRing + i;
                            var toIndex = rpc.ToSlotIndex * Gem.k_GemsPerRing + i;

                            if (fromIndex < equipped.Length && toIndex < equipped.Length)
                            {
                                (equipped[toIndex], equipped[fromIndex]) = (equipped[fromIndex], equipped[toIndex]);
                            }
                        }
                        
                        Debug.Log($"Swapped slots {rpc.FromSlotIndex} and {rpc.ToSlotIndex}");
                    }
                    SystemAPI.SetComponentEnabled<CompiledStatsDirty>(playerE, true);
                    break;
                }
                case GameRpc.Code.PlayerPickupRing:
                {
                    using var playerQuery = state.EntityManager.CreateEntityQuery(typeof(PlayerControlled), typeof(Ring), typeof(LocalTransform));
                    playerQuery.SetSharedComponentFilter(playerTag);
                    if (!playerQuery.HasSingleton<Ring>()) continue;
                    
                    var playerE = playerQuery.GetSingletonEntity();
                    var playerT = SystemAPI.GetComponent<LocalTransform>(playerE);
                    var rings = SystemAPI.GetBuffer<Ring>(playerE);

                    if (rpc.ToSlotIndex < 0 || rpc.ToSlotIndex >= rings.Length)
                    {
                        Debug.LogWarning($"Player {playerId} attempted to slot into index {rpc.ToSlotIndex} but only has {rings.Length} slots.");
                        continue;
                    }
                    
                    // Find the nearest interactable to our interact location
                    float bestD = float.MaxValue;
                    RingStats bestR = default;
                    Entity bestE = default;
                    foreach (var (interactableT, interactableRing, interactableE) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<RingStats>>().WithAll<Interactable>().WithEntityAccess())
                    {
                        var d = math.distancesq(interactableT.ValueRO.Position, playerT.Position);
                        if (d < bestD)
                        {
                            bestD = d;
                            bestR = interactableRing.ValueRO;
                            bestE = interactableE;
                        }
                    }
                    //if (bestD > FindNearestInteractableSystem.k_PickupRange) // Max interact range
                    //{
                    //    continue; // Too far away
                    //}

                    // Equip
                    (rings.ElementAt(rpc.ToSlotIndex).Stats, bestR) = (bestR, rings[rpc.ToSlotIndex].Stats);
                    Debug.Log($"Picked up ring into slot {rpc.ToSlotIndex}");
                    
                    var dirty = SystemAPI.GetComponent<CompiledStatsDirty>(playerE);
                    dirty.SetDirty(rings[rpc.ToSlotIndex]);
                    dirty.SetDirty(bestR);
                    SystemAPI.SetComponent(playerE, dirty);
                    
                    // Drop the item
                    if (bestR.IsValid)
                    {
                        var ringDropTemplate = SystemAPI.GetSingleton<GameManager.Resources>().RingDropTemplate;
                        var ringDropE = ecb.Instantiate(ringDropTemplate);
                        ecb.SetComponent(ringDropE, bestR);
                        ecb.SetComponent(ringDropE, playerT);
                        //ecb.SetSharedComponent(ringDropE, 
                        //    new InstancedResourceRequest(
                        //        SystemAPI.GetSingletonBuffer<GameManager.RingVisual>(true)[(int)bestR.PrimaryEffect].InstancedResourceIndex));
                    }
                    
                    
                    var key = state.EntityManager.GetSharedComponent<LootKey>(bestE);
                    if (key.Value != 0)
                    {
                        Debug.Log($"Destroying shared loot key {key.Value}");
                        var destroyQuery = state.EntityManager.CreateEntityQuery(typeof(Interactable), typeof(LootKey));
                        destroyQuery.SetSharedComponentFilter(key);
                        state.EntityManager.DestroyEntity(destroyQuery);
                    }
                    else
                        state.EntityManager.DestroyEntity(bestE);
                    
                    SystemAPI.SetComponentEnabled<CompiledStatsDirty>(playerE, true);
                    break;
                }
                case GameRpc.Code.PlayerDropRing:
                {
                    using var playerQuery = state.EntityManager.CreateEntityQuery(typeof(PlayerControlled), typeof(Ring), typeof(LocalTransform));
                    playerQuery.SetSharedComponentFilter(playerTag);
                    if (!playerQuery.HasSingleton<Ring>()) continue;
                    
                    var playerE = playerQuery.GetSingletonEntity();
                    var playerT = SystemAPI.GetComponent<LocalTransform>(playerE);
                    var rings = SystemAPI.GetBuffer<Ring>(playerE);

                    if (rpc.ToSlotIndex < 0 || rpc.ToSlotIndex >= rings.Length)
                    {
                        Debug.LogWarning($"Player {playerId} attempted to drop index {rpc.ToSlotIndex} but only has {rings.Length} slots.");
                        continue;
                    }
                    
                    var dropped = rings[rpc.ToSlotIndex];
                    rings[rpc.ToSlotIndex] = default;
                    if (dropped.Stats.IsValid)
                    {
                        var dirty = SystemAPI.GetComponent<CompiledStatsDirty>(playerE);
                        dirty.SetDirty(dropped);
                        SystemAPI.SetComponent(playerE, dirty);
                        
                        var ringDropTemplate = SystemAPI.GetSingleton<GameManager.Resources>().RingDropTemplate;
                        var ringDropE = ecb.Instantiate(ringDropTemplate);
                        ecb.SetComponent(ringDropE, dropped.Stats);
                        ecb.SetComponent(ringDropE, playerT);
                        SystemAPI.SetComponentEnabled<CompiledStatsDirty>(playerE, true);
                    }
                    break;
                }
                case GameRpc.Code.PlayerLevelStat:
                case GameRpc.Code.AdminPlayerLevelStat:
                {
                    using var playerQuery = state.EntityManager.CreateEntityQuery(typeof(PlayerControlled), typeof(TiledStatsTree), typeof(Wallet));
                    playerQuery.SetSharedComponentFilter(playerTag);
                    if (!playerQuery.HasSingleton<TiledStatsTree>()) continue;
                    
                    var playerE = playerQuery.GetSingletonEntity();
                    var stats = playerQuery.GetSingleton<TiledStatsTree>();
                    if (stats[rpc.AffectedStat] >= 5 && rpc.Type != GameRpc.Code.AdminPlayerLevelStat)
                    {
                        Debug.LogWarning($"Player {playerId} attempted to level up {rpc.AffectedStat} but it was already max level.");
                        continue;
                    }
                    if (!stats.CanLevelUp(rpc.AffectedStat) && rpc.Type != GameRpc.Code.AdminPlayerLevelStat)
                    {
                        Debug.LogWarning($"Player {playerId} attempted to level up {rpc.AffectedStat} but it's locked.");
                        continue;
                    }
                    
                    var wallet = playerQuery.GetSingleton<Wallet>();
                    
                    var cost = stats.GetLevelUpCost(rpc.AffectedStat);
                    if (cost > wallet.Value && rpc.Type != GameRpc.Code.AdminPlayerLevelStat)
                    {
                        Debug.LogWarning($"Player {playerId} didn't have enough money to level up {rpc.AffectedStat}: {wallet.Value}/{cost}.");
                        continue;
                    }
                    
                    wallet.Value = math.max(0, wallet.Value - cost);
                    
                    if (rpc.Type == GameRpc.Code.AdminPlayerLevelStat && rpc.ShouldLowerStat)
                        stats[rpc.AffectedStat]--;
                    else
                        stats[rpc.AffectedStat]++;
                    
                    // Gotta make this changes INSTANTLY, ecb is too slow.
                    SystemAPI.SetComponent(playerE, stats);
                    SystemAPI.SetComponent(playerE, wallet);
                    SystemAPI.SetComponentEnabled<CompiledStatsDirty>(playerE, true);
                    GameEvents.Trigger(GameEvents.Type.WalletChanged, playerE);
                    break;
                }
                
                case GameRpc.Code.AdminPlaceEnemy:
                {
                    var enemies = SystemAPI.GetSingletonBuffer<GameManager.Enemies>();
                    if (rpc.SpawnType < 0 || rpc.SpawnType >= enemies.Length)
                    {
                        Debug.LogWarning($"{rpc.SpawnType} is not a valid enemy.");
                        break;
                    }
                    
                    var enemy = state.EntityManager.Instantiate(enemies[rpc.SpawnType].Entity);
                    state.EntityManager.SetComponentData(enemy, LocalTransform.FromPosition(rpc.PlacePosition));
                    var enemySpawnOptions = (GameRpc.EnemySpawnOptions)rpc.SpawnOptions;
                    if (enemySpawnOptions.HasFlag(GameRpc.EnemySpawnOptions.Stationary))
                        state.EntityManager.SetComponentData(enemy, PhysicsResponse.Stationary);
                    if (enemySpawnOptions.HasFlag(GameRpc.EnemySpawnOptions.NoAi))
                        state.EntityManager.SetComponentData(enemy, new MovementSettings(){ Speed = 0 });
                    if (enemySpawnOptions.HasFlag(GameRpc.EnemySpawnOptions.InfiniteHealth))
                        state.EntityManager.SetComponentData(enemy, new Health(){ Value = int.MaxValue });
                    break;
                }
                
                case GameRpc.Code.AdminPlaceGem:
                {
                    var gemTemplate = SystemAPI.GetSingleton<GameManager.Resources>().GemDropTemplate;
                    var gemE = ecb.Instantiate(gemTemplate);
                    
                    var r = SystemAPI.GetSingleton<SharedRandom>().Random;
                    var gem = Gem.Generate(ref r);
                    Debug.Log($"Generated: {gem.GemType} gem");
                    Gem.SetupEntity(gemE, 0, ref r, ref ecb, LocalTransform.FromPosition(rpc.PlacePosition), default,  gem, SystemAPI.GetSingletonBuffer<GameManager.GemVisual>(true));
                    break;
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}