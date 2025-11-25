using BovineLabs.Saving;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[Save]
public struct ColorBaseColor : IComponentData
{
    public float4 Value;
    
    public ColorBaseColor(Color value)
    {
        Value = (Vector4)value;
    }
}
[Save]
public struct ColorA : IComponentData
{
    public float4 Value;
    
    public ColorA(Color value)
    {
        Value = (Vector4)value;
    }
}
[Save]
public struct ColorB : IComponentData
{
    public float4 Value;
    
    public ColorB(Color value)
    {
        Value = (Vector4)value;
    }
}
[Save]
public struct ColorC : IComponentData
{
    public float4 Value;
    
    public ColorC(Color value)
    {
        Value = (Vector4)value;
    }
}