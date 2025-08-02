using UnityEngine;

public class GameInitialize : MonoBehaviour
{
    private void Awake()
    {
        GameEvents.Initialize();
    }

    private void OnDestroy()
    {
        GameEvents.Dispose();
    }
}