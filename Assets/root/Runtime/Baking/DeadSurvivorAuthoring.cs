using Drawing;
using Unity.Entities;
using UnityEngine;

public class DeadSurvivorAuthoring : MonoBehaviourGizmos
{
    partial class Baker : Baker<DeadSurvivorAuthoring>
    {
        public override void Bake(DeadSurvivorAuthoring authoring)
        {
            if (!DependsOn(GetComponent<StepInputAuthoring>()))
            {
                Debug.LogError($"Cannot bake {authoring.gameObject}. Requires component: {nameof(StepInputAuthoring)}");
                return;
            }
            
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            
            AddComponent(entity, new PlayerControlledSaveable(){ Index = byte.MaxValue });
        }
    }
}