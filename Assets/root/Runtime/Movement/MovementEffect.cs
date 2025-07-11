using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct MovementEffect : IComponentData
{
    public Entity Target;
    public float2 Amount;
    public float Time;
    [ReadOnly] public float Duration;
    [ReadOnly] public Mode Type;
    [ReadOnly] public RelativeHeading Heading;
    
    public enum Mode
    {
        Constant,
        LinearDecreasing,
        ExponentialDecreasing
    }
    
    public enum RelativeHeading
    {
        Global,
        Local
    }
}