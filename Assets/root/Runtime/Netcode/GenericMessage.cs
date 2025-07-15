using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[StructLayout(layoutKind: LayoutKind.Sequential)]
public struct GenericMessage
{
    public byte Type;
    public byte Data;

    public void Execute(Game game)
    {
        switch (Type)
        {
            default:
                game.PlayerIndex = Data;
                break;
        }
    }
    
    public static GenericMessage Id(int index)
    {
        return new GenericMessage()
        {
            Type = 0,
            Data = (byte)index
        };
    } 

    public static unsafe GenericMessage Read(ref DataStreamReader reader)
    {
        GenericMessage result = new();
        result.Type = reader.ReadByte();
        result.Data = reader.ReadByte();
        return result;
    }
    
    public unsafe void Write(ref DataStreamWriter writer)
    {
        writer.WriteByte(Type);
        writer.WriteByte(Data);
    }
}