using System;
using Unity.Mathematics;
using UnityEngine;

public interface IStepProvider
{
    float HalfTime { get; }
    bool CanStep(Game game);
    void ExecuteStep();
}

public class ManualStepProvider : IStepProvider
{
    public float HalfTime => 0;
    public long ManualStep = 0;

    public void AdvanceStep()
    {
        ManualStep++;
    }
    public bool CanStep(Game game)
    {
        return game.Step < ManualStep;
    }

    public void ExecuteStep()
    {
    }
}

public class RateStepProvider : IStepProvider
{
    public float HalfTime => (float)math.clamp((Time.timeAsDouble - m_NextStepTime + Game.DefaultDt)/Game.DefaultDt, 0, 1);
    double m_NextStepTime;
    
    public bool CanStep(Game game)
    {
        return m_NextStepTime <= Time.timeAsDouble;
    }

    public void ExecuteStep()
    {
        // Doing this ensures that we don't execute more than 2 steps in a row
        m_NextStepTime = math.clamp(m_NextStepTime + Game.DefaultDt, Time.timeAsDouble - Game.DefaultDt, Time.timeAsDouble + Game.DefaultDt);
    }
}