using Unity.Entities;
using UnityEngine;

public class GameEventAuthoring : MonoBehaviour
{
    partial class Baker : Baker<GameEventAuthoring>
    {
        public override void Bake(GameEventAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
        }
    }
}