using UnityEngine;

public class ToggleOnPlayerDeath : MonoBehaviour
{
    public bool ShowOnDeath;
    
    private void Awake()
    {
        GameEvents.OnEvent += OnGameEvent;
        if (ShowOnDeath)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        GameEvents.OnEvent -= OnGameEvent;
    }

    private void OnGameEvent(GameEvents.Data data)
    {
        // Check for local player
        var eType = data.Type;
        var playerIndex = data.Int0; 
        if (playerIndex != Game.ClientGame.PlayerIndex) return;
        
        if (eType == GameEvents.Type.PlayerDied)
        {
            gameObject.SetActive(ShowOnDeath);
        }

        if (eType == GameEvents.Type.PlayerRevived)
        {
            gameObject.SetActive(!ShowOnDeath);
        }
    }
}