using Unity.Entities;
using UnityEngine;

public class ToggleOnPlayerDeath : MonoBehaviour
{
    public bool ShowOnDeath;
    
    private void Awake()
    {
        GameEvents.OnPlayerDied += OnPlayerDied;
        GameEvents.OnPlayerRevived += OnPlayerRevived;
        if (ShowOnDeath)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        GameEvents.OnPlayerDied += OnPlayerDied;
        GameEvents.OnPlayerRevived += OnPlayerRevived;
    }

    private void OnPlayerDied(Entity entity, int playerIndex)
    {
        // Check for local player
        if (entity != CameraTarget.MainEntity) return;
        gameObject.SetActive(ShowOnDeath);
    }
    private void OnPlayerRevived(Entity entity, int playerIndex)
    {
        // Check for local player
        if (entity != CameraTarget.MainEntity) return;
        gameObject.SetActive(!ShowOnDeath);
    }
}