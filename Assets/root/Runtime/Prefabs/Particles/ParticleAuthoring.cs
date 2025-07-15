using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class ParticleAuthoring : MonoBehaviour
{
    public class Baker : Baker<ParticleAuthoring>
    {
        public override void Bake(ParticleAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
        }
    }
}

public class ParticleDatabase : Database<ParticleAuthoring>
{
}