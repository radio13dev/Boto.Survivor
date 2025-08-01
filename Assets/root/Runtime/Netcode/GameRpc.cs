using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[Serializable]
public readonly struct GameRpc : IComponentData
{
    public enum Code
    {
        // Server Actions
        PlayerJoin = 0b1000_0000, // Server actions (that players cannot call directly) are flagged with this bit
        PlayerLeave = 0b1000_0001,

        // Runtime Actions
        PlayerAdjustInventory = 0b0000_0000,
    }

    [SerializeField] readonly byte m_Type;
    [SerializeField] readonly ulong m_Data0;
    [SerializeField] readonly float3 m_Data1;

    public bool IsValidClientRpc => (m_Type & (byte)Code.PlayerJoin) != 0;
    public Code Type => (Code)m_Type;
    public byte PlayerId => (byte)(m_Data0 & 0xFF);

    private GameRpc(byte type, ulong data0, float3 data1)
    {
        this.m_Type = type;
        this.m_Data0 = data0;
        this.m_Data1 = data1;
    }

    public GameRpc(Code type, ulong data, float3 data1 = default)
    {
        this.m_Type = (byte)type;
        this.m_Data0 = data;
        this.m_Data1 = data1;
    }

    public void Write(ref DataStreamWriter writer)
    {
        writer.WriteByte(m_Type);
        writer.WriteULong(m_Data0);
        writer.WriteFloat(m_Data1.x);
        writer.WriteFloat(m_Data1.y);
        writer.WriteFloat(m_Data1.z);
    }

    public static GameRpc Read(ref DataStreamReader reader)
    {
        GameRpc result = new(
            reader.ReadByte(), 
            reader.ReadULong(), 
            new float3(reader.ReadFloat(),reader.ReadFloat(),reader.ReadFloat())
        );
        return result;
    }

    public void Apply(Game game)
    {
        var rpc = game.World.EntityManager.CreateEntity(new ComponentType(typeof(GameRpc)));
        game.World.EntityManager.SetComponentData(rpc, this);
    }

    #region Inventory Adjust
    public byte From => (byte)((m_Data0 & 0x00_00_FF_00) >> 8);
    public byte To => (byte)((m_Data0 & 0x00_FF_00_00) >> 16);
    public bool IsToFloor => To == byte.MaxValue;
    public bool IsFromFloor => From >= Ring.k_RingCount;
    public int FromFloorIndex => From - Ring.k_RingCount;

    public float3 GetFromFloorPosition() => m_Data1;

    public static GameRpc PlayerAdjustInventory(byte player, byte from, byte to, float3 toPosition = default)
    {
        return new GameRpc(Code.PlayerAdjustInventory, (ulong)to << 16 | (ulong)from << 8 | (ulong)player, toPosition);
    }

    public static byte GetFloorIndexByte(int index)
    {
        return (byte)(Ring.k_RingCount + index);
    }
    #endregion
}

/// <summary>
/// RPCs are executed in the initialization system group because their data can't be saved.
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct GameRpcSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SharedRandom>();
        state.RequireForUpdate<GameManager.Resources>();
        state.RequireForUpdate<GameRpc>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp, PlaybackPolicy.SinglePlayback);
        foreach (var (rpcRO, rpcE) in SystemAPI.Query<RefRO<GameRpc>>().WithEntityAccess())
        {
            ecb.DestroyEntity(rpcE);

            var rpc = rpcRO.ValueRO;
            var playerId = rpc.PlayerId;
            var playerTag = new PlayerControlled() { Index = playerId };
            switch (rpc.Type)
            {
                case GameRpc.Code.PlayerJoin:
                {
                    using var playerQuery = state.EntityManager.CreateEntityQuery(typeof(PlayerControlled));
                    playerQuery.SetSharedComponentFilter(playerTag);
                    if (playerQuery.CalculateEntityCount() > 0)
                    {
                        Debug.Log($"Player {playerId} already exists, not creating a new one...");
                        continue;
                    }

                    var resources = SystemAPI.GetSingleton<GameManager.Resources>();
                    var newPlayer = ecb.Instantiate(resources.SurvivorTemplate);
                    ecb.AddSharedComponent<PlayerControlled>(newPlayer, playerTag);
                    ecb.SetComponent(newPlayer, new PlayerControlledSaveable(){ Index = playerTag.Index });
                    ecb.SetComponent(newPlayer, LocalTransform.FromPosition(20, 0, 0));
                    break;
                }
                case GameRpc.Code.PlayerLeave:
                {
                    using var playerQuery = state.EntityManager.CreateEntityQuery(typeof(PlayerControlled));
                    playerQuery.SetSharedComponentFilter(playerTag);
                    ecb.DestroyEntity(playerQuery, EntityQueryCaptureMode.AtPlayback);
                    break;
                }
                case GameRpc.Code.PlayerAdjustInventory:
                {
                    using var playerQuery = state.EntityManager.CreateEntityQuery(typeof(PlayerControlled), typeof(Ring), typeof(LocalTransform));
                    playerQuery.SetSharedComponentFilter(playerTag);
                    if (!playerQuery.HasSingleton<Ring>()) continue;
                    
                    var playerE = playerQuery.GetSingletonEntity();
                    var inventory = SystemAPI.GetBuffer<Ring>(playerE);
                    var playerT = SystemAPI.GetComponent<LocalTransform>(playerE);

                    if (rpc.IsToFloor)
                    {
                        // Validate the 'from' movement
                        var from = rpc.From;
                        if (from >= inventory.Length)
                            continue; // Invalid 'from' index

                        if (inventory[from].Stats.PrimaryEffect == RingPrimaryEffect.None)
                            continue; // Nothing to drop

                        // Remove item from inventory
                        var taken = inventory[from].Stats;
                        inventory[from] = default;

                        //  and spawn it in world
                        var resources = SystemAPI.GetSingleton<GameManager.Resources>();
                        var ringDrop = ecb.Instantiate(resources.ItemDropTemplate);
                        ecb.SetComponent(ringDrop, playerT);
                        ecb.SetComponent(ringDrop, new Movement() { Velocity = SystemAPI.GetSingleton<SharedRandom>().Random.NextFloat3Direction() * 2f });
                        ecb.SetComponent(ringDrop, taken);

                        Debug.Log($"{from} from hand to floor");
                    }
                    else if (rpc.IsFromFloor)
                    {
                        // Validate the 'to' movement
                        var to = rpc.To;
                        if (to >= inventory.Length)
                            continue; // Invalid 'to' index

                        // Validate the 'pickup' exists
                        Entity pickupE = Entity.Null;
                        float pickupD = float.MaxValue;
                        foreach (var (testPickupT, testPickupE) in SystemAPI.Query<RefRO<LocalTransform>>().WithAny<RingStats, LootGenerator2>().WithEntityAccess())
                        {
                            var d = math.distancesq(testPickupT.ValueRO.Position, rpc.GetFromFloorPosition());
                            if (d < pickupD)
                            {
                                pickupE = testPickupE;
                                pickupD = d;
                            }
                            else if (d == pickupD)
                            {
                                // Desync!
                                Debug.LogError($"Desync occured during item pickup!");
                            }
                        }

                        if (pickupD > 2 * 2) continue; // Too far away

                        // Pick it up and destroy it
                        RingStats pickup;
                        if (SystemAPI.HasComponent<LootGenerator2>(pickupE))
                            pickup = SystemAPI.GetComponent<LootGenerator2>(pickupE).GetRingStats(rpc.FromFloorIndex);
                        else
                            pickup = SystemAPI.GetComponent<RingStats>(pickupE);

                        ecb.DestroyEntity(pickupE);

                        // Remove item from inventory
                        var taken = inventory[to].Stats;
                        inventory[to] = new Ring() { Stats = pickup };
                        Debug.Log($"{rpc.FromFloorIndex} from floor to {to}");

                        // and spawn it in world
                        if (taken.PrimaryEffect != RingPrimaryEffect.None)
                        {
                            var resources = SystemAPI.GetSingleton<GameManager.Resources>();
                            var ringDrop = ecb.Instantiate(resources.ItemDropTemplate);
                            ecb.SetComponent(ringDrop, SystemAPI.GetComponent<LocalTransform>(playerE));
                            ecb.SetComponent(ringDrop, new Movement() { Velocity = SystemAPI.GetSingleton<SharedRandom>().Random.NextFloat3Direction() * 2f });
                            ecb.SetComponent(ringDrop, taken);
                            Debug.Log($"{to} from hand to floor");
                        }
                    }
                    else
                    {
                        var from = rpc.From;
                        var to = rpc.To;

                        // Validate the 'from' and 'to' movement
                        if (from >= inventory.Length)
                            continue; // Invalid 'from' index
                        if (to >= inventory.Length)
                            continue; // Invalid 'to' index

                        (inventory[from], inventory[to]) = (inventory[to], inventory[from]);
                        Debug.Log($"{from} from hand to {to}");
                    }

                    SystemAPI.SetComponentEnabled<CompiledStatsDirty>(playerE, true);
                    break;
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}