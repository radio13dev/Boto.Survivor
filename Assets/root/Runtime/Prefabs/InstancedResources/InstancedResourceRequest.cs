using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Jobs;
using Object = UnityEngine.Object;

public struct InstancedResourceRequest : ISharedComponentData
{
    public readonly int ToSpawn;

    public InstancedResourceRequest(int toSpawn)
    {
        ToSpawn = toSpawn;
    }
}