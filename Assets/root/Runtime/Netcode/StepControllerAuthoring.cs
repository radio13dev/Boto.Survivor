using BovineLabs.Saving;
using Unity.Entities;
using UnityEngine;

public class StepControllerAuthoring : MonoBehaviour
{
    partial class Baker : Baker<StepControllerAuthoring>
    {
        public override void Bake(StepControllerAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent<StepController>(entity);
        }
    }
}

[Save]
public struct StepController : IComponentData
{
    public long Step;

    public StepController(long step)
    {
        Step = step;
    }
}