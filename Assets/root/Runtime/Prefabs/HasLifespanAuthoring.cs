using BovineLabs.Saving;
using Unity.Entities;
using UnityEngine;

[Save]
public struct SpawnTimeCreated : IComponentData
{
    public double TimeCreated;

    public SpawnTimeCreated(double time)
    {
        TimeCreated = time;
    }
}

[Save]
public struct DestroyAtTime : IComponentData
{
    public double DestroyTime;

    public DestroyAtTime(double time)
    {
        DestroyTime = time;
    }
}

public partial class HasLifespanAuthoring : MonoBehaviour
{
    partial class Baker : Baker<HasLifespanAuthoring>
    {
        public override void Bake(HasLifespanAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new SpawnTimeCreated());
            AddComponent(entity, new DestroyAtTime(){ DestroyTime = double.MaxValue });
        }
    }
}