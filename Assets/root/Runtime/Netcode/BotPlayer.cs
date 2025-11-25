using BovineLabs.Core.Extensions;
using Unity.Mathematics;

public class BotPlayer
{
    public readonly byte PlayerIndex;
    StepInput m_LockedInputs;

    public BotPlayer(byte playerIndex)
    {
        PlayerIndex = playerIndex;
    }

    // Advances logic. Decisions are only made every so often for performance reasons.
    public void Advance(Game game, float dt)
    {
        var step = game.World.EntityManager.GetSingleton<StepController>().Step;
        if ((step % 60) == 0)
        {
            m_LockedInputs = new StepInput()
            {
                Direction = Random.CreateFromIndex((uint)step).NextFloat3Direction()
            };
        }
    }

    // Returns the current set of inputs
    public StepInput GetInputs(Game game)
    {
        return m_LockedInputs;
    }
}