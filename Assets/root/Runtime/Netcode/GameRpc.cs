using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Serialization;
using Collider = Collisions.Collider;
using Random = Unity.Mathematics.Random;

[Save]
[Serializable]
[StructLayout(LayoutKind.Explicit, Size = GameRpc.Length, Pack = 2)]
public unsafe struct GameRpc : IComponentData
{
    public enum Code : byte
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
        PlayerSellRing = 0b0000_1000,
        PlayerOpenLobby = 0b0000_1001,
        PlayerInviteToPrivateLobby = 0b0000_1010,
        PlayerSetLobbyPrivate = 0b0000_1011,

        
        // Admin Actions
        _AdminActionBit = 0b0100_0000,
        AdminPlaceEnemy = 0b0100_0000, // Admin action flag
        AdminPlaceGem = 0b0100_0001,
        AdminPlayerLevelStat = 0b0100_0010,
        AdminPlaceRing = 0b0100_0011,
    }

    public const int Length = sizeof(long)*2;
    [SerializeField] [FieldOffset(0)] long m_WRITE0;
    [SerializeField] [FieldOffset(8)] long m_WRITE1;

    [FieldOffset(0)] byte m_Type;
    [FieldOffset(1)] byte m_Player;
    [FieldOffset(0)] fixed byte m_Data[Length];
    public bool IsValidClientRpc => (m_Type & (byte)Code._ServerActionBit) == 0;
    public bool IsAdminRpc => (m_Type & (byte)Code._AdminActionBit) != 0;

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
    
    #region PlayerOpenLobby
    [FieldOffset(2)] public bool IsPrivateLobby;
    public static GameRpc PlayerOpenLobby(byte playerId, float3 position, bool isPrivateLobby)
    {
        return new GameRpc(){ Type = GameRpc.Code.PlayerOpenLobby, PlayerId = playerId, InteractPosition = position, IsPrivateLobby = isPrivateLobby };
    }
    [FieldOffset(2)] public byte InvitedPlayerId;
    public static GameRpc PlayerInviteToPrivateLobby(byte playerId, byte otherPlayerId)
    {
        return new GameRpc(){ Type = GameRpc.Code.PlayerInviteToPrivateLobby, PlayerId = playerId, InvitedPlayerId = otherPlayerId };
    }
    public static GameRpc PlayerSetLobbyPrivate(byte playerId, bool setPrivate)
    {
        return new GameRpc(){ Type = GameRpc.Code.PlayerSetLobbyPrivate, PlayerId = playerId, IsPrivateLobby = setPrivate };
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
    public static GameRpc PlayerSellRing(byte player, float3 interactPosition)
    {
        return new GameRpc() { Type = Code.PlayerSellRing, PlayerId = player, FromSlotIndex = byte.MaxValue, InteractPosition = interactPosition};
    }
    public static GameRpc PlayerSellRing(byte player, byte fromSlotIndex)
    {
        return new GameRpc() { Type = Code.PlayerSellRing, PlayerId = player, FromSlotIndex = fromSlotIndex};
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
    public static GameRpc AdminPlaceRing(byte player, Vector3 placePosition)
    {
        return new GameRpc() { Type = Code.AdminPlaceRing, PlayerId = player, PlacePosition = placePosition };
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
                    if (playerId >= PingServerBehaviour.k_MaxPlayerCount)
                    {
                        Debug.Log($"Player {playerId} is invalid, too many connections");
                        continue;
                    }
                
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
                
                case GameRpc.Code.PlayerOpenLobby:
                {
                    if (SystemAPI.GetSingleton<GameTypeSingleton>().Value != 0)
                    {
                        Debug.LogWarning($"Can only create a lobby from the lobby game mode.");
                        break;
                    }
                
                    using var playerQuery = state.EntityManager.CreateEntityQuery(typeof(PlayerControlled), typeof(LocalTransform));
                    playerQuery.SetSharedComponentFilter(playerTag);
                    if (!playerQuery.HasSingleton<LocalTransform>()) continue;
                    
                    var playerT = playerQuery.GetSingleton<LocalTransform>();
                    playerT.Position = TorusMapper.SnapToSurface(playerT.Position);
                    
                    // Attempt to open a lobby in the nearest 'free space' to the player
                    using var lobbyQuery = state.EntityManager.CreateEntityQuery(typeof(LobbyZone), typeof(LocalTransform), typeof(Collisions.Collider));
                    using var lobbyTransforms = lobbyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                    using var lobbyColliders = lobbyQuery.ToComponentDataArray<Collisions.Collider>(Allocator.Temp);
                    
                    // We can 'shift' away once, but will just fail if it happens a second time.
                    var potentialLobbyC = Collider.Sphere(LobbyZone.ColliderRadius).Apply(playerT);
                    bool didShift = false;
                    bool didFail = false;
                    for (int i = 0; i < lobbyTransforms.Length; i++)
                    {
                        var lobbyC = lobbyColliders[i].Apply(lobbyTransforms[i]);
                        if (!potentialLobbyC.Overlaps(lobbyC)) continue;
                        
                        if (didShift)
                        {
                            didFail = true;
                            break;
                        }
                        
                        // Shift away from the center of the lobby
                        var surfacePoint = lobbyC.GetPointOnSurface(potentialLobbyC);
                        var shift = (surfacePoint - potentialLobbyC.Center)*1.2f; // A little extra movement
                        playerT = playerT.Translate(shift);
                        playerT.Position = TorusMapper.SnapToSurface(playerT.Position);
                        potentialLobbyC = Collider.Sphere(LobbyZone.ColliderRadius).Apply(playerT);
                        
                        didShift = true;
                        i = -1; // Restart the loop
                    }
                    
                    if (didFail)
                    {
                        Debug.LogWarning($"No space to create lobby for player {playerId} at {rpc.InteractPosition}, too close to other lobbies.");
                        break;
                    }
                    
                    // Create the lobby at this point
                    var lobbyTemplates = SystemAPI.GetSingletonBuffer<GameManager.Prefabs>(true)[GameManager.Prefabs.LobbyTemplate];
                    var lobbyE = ecb.Instantiate(lobbyTemplates.Entity);
                    ecb.SetComponent(lobbyE, new LobbyZone(){ IsPrivate = rpc.IsPrivateLobby, Owner = playerId });
                    ecb.SetComponent(lobbyE, playerT);
                    ecb.SetBuffer<PrivateLobbyWhitelist>(lobbyE).Add(new PrivateLobbyWhitelist(){ Player = playerId });
                    break;
                }
                case GameRpc.Code.PlayerInviteToPrivateLobby:
                {
                    if (SystemAPI.GetSingleton<GameTypeSingleton>().Value != 0)
                    {
                        Debug.LogWarning($"Can only interact with lobbies from the lobby game mode.");
                        break;
                    }
                
                    using var playerQuery = state.EntityManager.CreateEntityQuery(typeof(PlayerControlled), typeof(LocalTransform));
                    playerQuery.SetSharedComponentFilter(playerTag);
                    if (!playerQuery.HasSingleton<LocalTransform>()) continue;
                    
                    var playerT = playerQuery.GetSingleton<LocalTransform>();
                    playerT.Position = TorusMapper.SnapToSurface(playerT.Position);
                    
                    // Attempt to open a lobby in the nearest 'free space' to the player
                    using var lobbyQuery = state.EntityManager.CreateEntityQuery(typeof(LobbyZone), typeof(PrivateLobbyWhitelist));
                    using var lobbyEntities = lobbyQuery.ToEntityArray(Allocator.Temp);
                    using var lobbyComponents = lobbyQuery.ToComponentDataArray<LobbyZone>(Allocator.Temp);
                    for (int i = 0; i < lobbyEntities.Length; i++)
                    {
                        if (!lobbyComponents[i].IsPrivate) continue;
                        if (lobbyComponents[i].Owner != playerId) continue;
                        
                        
                        var whitelist = SystemAPI.GetBuffer<PrivateLobbyWhitelist>(lobbyEntities[i]);
                        bool contains = false;
                        for (int j = 0; j < whitelist.Length; j++)
                        {
                            if (whitelist[j].Player == rpc.InvitedPlayerId)
                            {
                                contains = true;
                                break;
                            }
                        }
                        if (!contains) whitelist.Add(new PrivateLobbyWhitelist(){ Player = rpc.InvitedPlayerId });
                    }
                    break;
                }
                case GameRpc.Code.PlayerSetLobbyPrivate:
                {
                    using var lobbyQuery = state.EntityManager.CreateEntityQuery(typeof(LobbyZone), typeof(PrivateLobbyWhitelist));
                    using var lobbyEntities = lobbyQuery.ToEntityArray(Allocator.Temp);
                    using var lobbyComponents = lobbyQuery.ToComponentDataArray<LobbyZone>(Allocator.Temp);
                    for (int i = 0; i < lobbyEntities.Length; i++)
                    {
                        if (lobbyComponents[i].Owner != playerId) continue;
                        
                        SystemAPI.SetComponent(lobbyEntities[i], new LobbyZone(){ IsPrivate = rpc.IsPrivateLobby, Owner = lobbyComponents[i].Owner });
                        var buffer = SystemAPI.GetBuffer<PrivateLobbyWhitelist>(lobbyEntities[i]);
                        buffer.Clear();
                        buffer.Add(new PrivateLobbyWhitelist(){ Player = playerId });
                    }
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
                    
                    if (rpc.FromSlotIndex >= equipped.Length)
                    {
                        Debug.LogWarning($"Player {playerId} attempted to slot from index {rpc.FromSlotIndex} but only has {equipped.Length} slots.");
                        continue;
                    }
                    
                    if (rpc.ToSlotIndex == byte.MaxValue)
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
                        if (rpc.ToSlotIndex >= equipped.Length)
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
                    
                    if (rpc.FromSlotIndex >= rings.Length)
                    {
                        Debug.LogWarning($"Player {playerId} attempted to slot from index {rpc.FromSlotIndex} but only has {rings.Length} slots.");
                        continue;
                    }
                    
                    if (rpc.ToSlotIndex == byte.MaxValue)
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
                        if (SystemAPI.HasComponent<Collectable>(interactableE) && SystemAPI.GetComponent<Collectable>(interactableE).PlayerId != playerId)
                            continue;
                            
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
                        var ringTemplates = SystemAPI.GetSingletonBuffer<GameManager.RingDropTemplate>(true);
                        var r = SystemAPI.GetSingleton<SharedRandom>().Random;
                        var ringE = ecb.Instantiate(ringTemplates[bestR.Tier].Entity);
                        Debug.Log($"Generated: {bestR} ring");
                        Ring.SetupEntity(ringE, playerId, ref r, ref ecb, playerT, default, bestR);
                        
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
                case GameRpc.Code.PlayerSellRing:
                {
                    using var playerQuery = state.EntityManager.CreateEntityQuery(typeof(PlayerControlled), typeof(Ring), typeof(LocalTransform));
                    playerQuery.SetSharedComponentFilter(playerTag);
                    if (!playerQuery.HasSingleton<Ring>()) continue;
                    
                    var playerE = playerQuery.GetSingletonEntity();
                    var playerT = SystemAPI.GetComponent<LocalTransform>(playerE);
                    var rings = SystemAPI.GetBuffer<Ring>(playerE);
                    
                    int earned = 0;
                    
                    if (rpc.FromSlotIndex >= Ring.k_RingCount)
                    {
                        // Sell grounded pickup (if we are the owner)
                        // Find the nearest interactable to our interact location
                        float bestD = float.MaxValue;
                        RingStats bestR = default;
                        Entity bestE = default;
                        foreach (var (interactableT, interactableRing, interactableE) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<RingStats>>().WithAll<Interactable>().WithEntityAccess())
                        {
                            if (SystemAPI.HasComponent<Collectable>(interactableE) && SystemAPI.GetComponent<Collectable>(interactableE).PlayerId != playerId)
                                continue;
                            
                            var d = math.distancesq(interactableT.ValueRO.Position, playerT.Position);
                            if (d < bestD)
                            {
                                bestD = d;
                                bestR = interactableRing.ValueRO;
                                bestE = interactableE;
                            }
                        }
                        
                        if (bestE == Entity.Null)
                        {
                            Debug.LogWarning($"Player {playerId} couldn't find the ring they were looking for at {playerT.Position}.");
                            continue;
                        }
                        
                        earned +=  bestR.GetSellPrice();
                        
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
                    }
                    else
                    {
                        var ring = rings[rpc.FromSlotIndex];
                        if (!ring.Stats.IsValid)
                        {
                            Debug.LogWarning($"Player {playerId} attempted to sell empty ring slot {rpc.FromSlotIndex}.");
                            continue;
                        }
                        
                        var dirty = SystemAPI.GetComponent<CompiledStatsDirty>(playerE);
                        dirty.SetDirty(rings[rpc.ToSlotIndex]);
                        dirty.SetDirty(ring);
                        SystemAPI.SetComponent(playerE, dirty);
                        SystemAPI.SetComponentEnabled<CompiledStatsDirty>(playerE, true);
                        
                        earned += ring.Stats.GetSellPrice();
                        rings[rpc.FromSlotIndex] = default;
                    }
                    
                    var gemTemplate = SystemAPI.GetSingleton<GameManager.Resources>().GemDropTemplate;
                    var r = SystemAPI.GetSingleton<SharedRandom>().Random;
                    for (int i = 0; i < earned; i++)
                        CreateGem(ref ecb, playerId, gemTemplate, ref r, playerT.Position + r.NextFloat3Direction()*r.NextFloat()*4);
                    
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
                        SystemAPI.SetComponentEnabled<CompiledStatsDirty>(playerE, true);
                        
                        var ringTemplates = SystemAPI.GetSingletonBuffer<GameManager.RingDropTemplate>(true);
                        var r = SystemAPI.GetSingleton<SharedRandom>().Random;
                        var ringE = ecb.Instantiate(ringTemplates[dropped.Stats.Tier].Entity);
                        Debug.Log($"Generated: {dropped.Stats} ring");
                        Ring.SetupEntity(ringE, playerId, ref r, ref ecb, playerT, default, dropped.Stats);
                    }
                    break;
                }
                case GameRpc.Code.PlayerLevelStat:
                case GameRpc.Code.AdminPlayerLevelStat:
                {
                    using var playerQuery = state.EntityManager.CreateEntityQuery(typeof(PlayerControlled), typeof(TiledStatsTree), typeof(Wallet), typeof(PlayerLevel));
                    playerQuery.SetSharedComponentFilter(playerTag);
                    if (!playerQuery.HasSingleton<TiledStatsTree>()) continue;
                    
                    var playerE = playerQuery.GetSingletonEntity();
                    var stats = playerQuery.GetSingleton<TiledStatsTree>();
                    if (stats[rpc.AffectedStat] >= 1 && !rpc.IsAdminRpc)
                    {
                        Debug.LogWarning($"Player {playerId} attempted to level up {rpc.AffectedStat} but it was already max level.");
                        continue;
                    }
                    
                    var playerLevel = playerQuery.GetSingleton<PlayerLevel>();
                    var levelsSpent = stats.GetLevelsSpent();
                    if (levelsSpent >= playerLevel.Level && !rpc.IsAdminRpc)
                    {
                        Debug.LogWarning($"Player {playerId} attempted to level up {rpc.AffectedStat} but they have already spent {levelsSpent}/{playerLevel.Level} levels");
                        continue;
                    }
                    
                    var wallet = playerQuery.GetSingleton<Wallet>();
                    
                    var cost = stats.GetLevelUpCost(rpc.AffectedStat);
                    if (cost > wallet.Value && !rpc.IsAdminRpc)
                    {
                        Debug.LogWarning($"Player {playerId} didn't have enough money to level up {rpc.AffectedStat}: {wallet.Value}/{cost}.");
                        continue;
                    }
                    
                    wallet.Value = math.max(0, wallet.Value - cost);
                    
                    if (rpc.IsAdminRpc && rpc.ShouldLowerStat)
                        stats[rpc.AffectedStat]--;
                    else
                        stats[rpc.AffectedStat]++;
                    
                    // Gotta make this changes INSTANTLY, ecb is too slow.
                    SystemAPI.SetComponent(playerE, stats);
                    SystemAPI.SetComponent(playerE, wallet);
                    SystemAPI.SetComponentEnabled<CompiledStatsDirty>(playerE, true);
                    var dirty = SystemAPI.GetComponent<CompiledStatsDirty>(playerE);
                    dirty.SetDirty();
                    SystemAPI.SetComponent(playerE, dirty);
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
                        state.EntityManager.SetComponentData(enemy, new Health(int.MaxValue));
                    break;
                }
                
                case GameRpc.Code.AdminPlaceGem:
                {
                    var gemTemplate = SystemAPI.GetSingleton<GameManager.Resources>().GemDropTemplate;
                    var r = SystemAPI.GetSingleton<SharedRandom>().Random;
                    CreateGem(ref ecb, playerId, gemTemplate, ref r, rpc.PlacePosition);
                    break;
                }
                case GameRpc.Code.AdminPlaceRing:
                {
                    var ringTemplates = SystemAPI.GetSingletonBuffer<GameManager.RingDropTemplate>(true);
                    
                    var r = SystemAPI.GetSingleton<SharedRandom>().Random;
                    var ring = RingStats.Generate(ref r);
                    var ringE = ecb.Instantiate(ringTemplates[ring.Tier].Entity);
                    Debug.Log($"Generated: {ring} ring");
                    Ring.SetupEntity(ringE, playerId, ref r, ref ecb, LocalTransform.FromPosition(rpc.PlacePosition), default, ring);
                    break;
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private void CreateGem(ref EntityCommandBuffer ecb, byte playerId, Entity gemTemplate, ref Random random, float3 placePosition)
    {
        var gemE = ecb.Instantiate(gemTemplate);
        var gem = Gem.Generate(ref random);
        Debug.Log($"Generated: {gem.GemType} gem");
        Gem.SetupEntity(gemE, playerId, ref random, ref ecb, LocalTransform.FromPosition(placePosition), default,  gem, SystemAPI.GetSingletonBuffer<GameManager.GemVisual>(true));
    }
}