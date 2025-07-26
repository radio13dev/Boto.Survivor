using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[Serializable]
public struct SpecialLockstepActions
{
    // @formatter:off
    public const byte CODE_PlayerJoin               = 0b0000_0001;
    public const byte CODE_PlayerLeave              = 0b0000_0010;
    
    // All rpcs have the 0b1000_0000 bit set
    public const byte CODE_Rpc_PlayerAdjustInventory= 0b1000_0000;
    // @formatter:on 
    
    public byte Type;
    public byte Data;
    public byte Extension;
    
    public bool IsValidClientRpc => Type >= 0b1000_0000;

    public void Write(ref DataStreamWriter writer)
    {
        writer.WriteByte(Type);
        writer.WriteByte(Data);
        writer.WriteByte(Extension);
    }

    public static SpecialLockstepActions Read(ref DataStreamReader reader)
    {
        SpecialLockstepActions result = new();
        result.Type = reader.ReadByte();
        result.Data = reader.ReadByte();
        result.Extension = reader.ReadByte();
        return result;
    }

    public void Apply(Game game)
    {
        var world = game.World;
        switch (Type)
        {
            case CODE_PlayerJoin:
                var resourcesQuery = world.EntityManager.CreateEntityQuery(new ComponentType(typeof(GameManager.Resources)));
                var resources = resourcesQuery.GetSingleton<GameManager.Resources>();
                var newPlayer = world.EntityManager.Instantiate(resources.SurvivorTemplate);
                world.EntityManager.AddComponent<PlayerControlled>(newPlayer);
                world.EntityManager.SetComponentData(newPlayer, new PlayerControlled(){ Index = Data });
                world.EntityManager.SetComponentData(newPlayer, LocalTransform.FromPosition(20,0,0));
                
                game.m_PlayerDataCaches.Add(new PlayerDataCache((int)Data));
                break;
                
            case CODE_PlayerLeave:
                var controlledQuery = world.EntityManager.CreateEntityQuery(new ComponentType(typeof(PlayerControlled)));
                var players = controlledQuery.ToComponentDataArray<PlayerControlled>(Allocator.Temp);
                var playersE = controlledQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i].Index == Data)
                    {
                        world.EntityManager.DestroyEntity(playersE[i]);
                    } 
                }
                players.Dispose();
                playersE.Dispose();
                break;
            
            case CODE_Rpc_PlayerAdjustInventory:
                var rpc = world.EntityManager.CreateEntity(new ComponentType(typeof(Rpc_PlayerAdjustInventory)));
                world.EntityManager.SetComponentData(rpc, new Rpc_PlayerAdjustInventory(Data, Extension));
                break;
        }
    }

    public static SpecialLockstepActions PlayerLeave(int i)
    {
        return new SpecialLockstepActions()
        {
            Type = CODE_PlayerLeave,
            Data = (byte)i
        };
    }

    public static SpecialLockstepActions PlayerJoin(int i)
    {
        return new SpecialLockstepActions()
        {
            Type = CODE_PlayerJoin,
            Data = (byte)i
        };
    }

    public static SpecialLockstepActions Rpc_PlayerAdjustInventory(int player, int from, int to)
    {
        return new SpecialLockstepActions()
        {
            Type = CODE_Rpc_PlayerAdjustInventory,
            Data = (byte)player,
            Extension = (byte)((from << 4) | (to & 0x0F)) // Store 'from' in the upper nibble and 'to' in the lower nibble
        };
    }
}