using UnityEngine;
using UnityEngine.Events;

public class ToggleOnDeath : MonoBehaviour
{
    [SerializeField] private UnityEvent _onLocalPlayerDeath;
        
    private void Awake()
    {
        GameEvents.OnEvent += OnGameEvent;
    }

    private void OnDestroy()
    {
        GameEvents.OnEvent -= OnGameEvent;
    }

    private void OnGameEvent(GameEvents.Data data)
    {
        var eType = data.Type;
        var playerIndex = data.Int0; 
        if (eType != GameEvents.Type.PlayerDied) return;
        if (playerIndex != Game.ClientGame.PlayerIndex) return;
        _onLocalPlayerDeath?.Invoke();
    }
    
    [EditorButton]
    public void SendBackToLobby()
    {
        var oldGame = GameLaunch.Main;
        var newGame = GameLaunch.Create(new LobbyFactory("game"));
        void Swap()
        {
            GameLaunch.Main = newGame;
            oldGame.Dispose();
            Game.OnClientGameChanged -= Swap;
        }
        Game.OnClientGameChanged += Swap;
    }
}