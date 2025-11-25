using UnityEngine;

public class LobbyExitButton : MonoBehaviour
{
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