using Unity.Entities;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    [EditorButton]
    public void DebugWorldInfo()
    {
        var worlds = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.LocalSimulation);
    }
}
