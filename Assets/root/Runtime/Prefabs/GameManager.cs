using System;
using Unity.Collections;
using Unity.Entities;

public static class GameManager
{
    [ChunkSerializable]
    public struct Resources : IComponentData
    {
        public Entity ProjectileTemplate;
    }
}