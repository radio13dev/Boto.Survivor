using Unity.Entities;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class SharedRandomAuthoring : MonoBehaviour
{
    partial class Baker : Baker<SharedRandomAuthoring>
    {
        public override void Bake(SharedRandomAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new SharedRandom(){ Random = Random.CreateFromIndex(0) });
        }
    }
}