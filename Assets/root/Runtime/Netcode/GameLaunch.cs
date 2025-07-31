using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public interface IGameBehaviour
{
    
}

public class GameLaunch : MonoBehaviour
{
    static GameLaunch _instance;

    private IEnumerator Start()
    {
        _instance = this;
        
        yield return new WaitUntil(() => Game.ConstructorReady);
        if (gameObject.GetComponents<IGameBehaviour>().Length > 0)
        {
            Debug.Log($"Game already launched, likely joined lobby.");
            yield break;
        }
        gameObject.AddComponent<SingleplayerBehaviour>();
    }
    
    public static void JoinLobby(string lobbyCode) => _instance._JoinLobby(lobbyCode);
    
    [EditorButton]
    public void _JoinLobby(string lobbyCode)
    {
        // Start a new game, attempting to load data from the lobby code
        var newClient = gameObject.AddComponent<PingClientBehaviour>();
        newClient.StartCoroutine(newClient.Connect(lobbyCode, 
        OnSuccess: () =>
        {
            // Destroy any existing games
            foreach (var gameBehaviour in gameObject.GetComponents<IGameBehaviour>())
            {
                if (gameBehaviour != newClient) Destroy((MonoBehaviour)gameBehaviour);
            }
        },
        OnFailure: () =>
        {
            Debug.LogError($"Failed to join lobby {lobbyCode}");
            Destroy(newClient);
            
            if (gameObject.GetComponents<IGameBehaviour>().Length == 0)
            {
                Debug.LogWarning($"No game running, launching singleplayer game as fallback...");
                gameObject.AddComponent<SingleplayerBehaviour>();
            }
        }));
    }
    
    static Task s_SignInTask;
    public static bool IsSignedIn { get; private set; }
    public static async Task SignIn()
    {
        if (s_SignInTask != null && !s_SignInTask.IsCompleted)
        {
            await s_SignInTask;
            return;
        }
        
        // First sign in
        if (!IsSignedIn)
        {
            s_SignInTask = UnityServices.InitializeAsync();
            await s_SignInTask;
            s_SignInTask = AuthenticationService.Instance.SignInAnonymouslyAsync();
            await s_SignInTask;
            s_SignInTask = null;
            IsSignedIn = true;
        }
        // Auth-service reconnect
        else if (AuthenticationService.Instance.IsExpired)
        {
            s_SignInTask = AuthenticationService.Instance.SignInAnonymouslyAsync();
            await s_SignInTask;
            s_SignInTask = null;
        }
    }
}
