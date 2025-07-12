using Unity.Collections;
using Unity.Entities;

public struct SpecialLockstepActions
{
    // @formatter:off
    public const byte CODE_PlayerJoin    = 0b0000_0001;
    public const byte CODE_PlayerLeave   = 0b0000_0010;
    public const byte _2            = 0b0000_0100;
    public const byte _3            = 0b0000_1000;
    
    public const byte _4            = 0b0001_0000;
    public const byte _5            = 0b0010_0000;
    public const byte _6            = 0b0100_0000;
    public const byte _7            = 0b1000_0000;
    // @formatter:on 
    
    public byte Type;
    public byte Data;
    
    public void Write(ref DataStreamWriter writer)
    {
        writer.WriteByte(Type);
        writer.WriteByte(Data);
    }

    public static SpecialLockstepActions Read(ref DataStreamReader reader)
    {
        SpecialLockstepActions result = new();
        result.Type = reader.ReadByte();
        result.Data = reader.ReadByte();
        return result;
    }

    public void Apply(World world)
    {
        switch (Type)
        {
            case CODE_PlayerJoin:
                var resourcesQuery = world.EntityManager.CreateEntityQuery(new ComponentType(typeof(GameManager.Resources)));
                var resources = resourcesQuery.GetSingleton<GameManager.Resources>();
                var newPlayer = world.EntityManager.Instantiate(resources.SurvivorTemplate);
                world.EntityManager.AddComponent<PlayerControlled>(newPlayer);
                world.EntityManager.SetComponentData(newPlayer, new PlayerControlled(){ Index = Data });
                break;
                
            case CODE_PlayerLeave:
                var controlledQuery = world.EntityManager.CreateEntityQuery(new ComponentType(typeof(PlayerControlled)));
                var controlled = controlledQuery.ToComponentDataArray<PlayerControlled>(Allocator.Temp);
                for (int i = 0; i < controlled.Length; i++)
                {
                    if (controlled[i].Index == Data)
                    {
                        var toDelete = controlledQuery.ToEntityArray(Allocator.Temp);
                        world.EntityManager.DestroyEntity(toDelete[i]);
                        toDelete.Dispose();
                        break;
                    }
                }
                controlled.Dispose();
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
}