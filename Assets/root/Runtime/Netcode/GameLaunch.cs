using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class GameHostBehaviour : MonoBehaviour
{
    
}

public class GameLaunch : MonoBehaviour
{
    static GameLaunch _instance;
    public static string LastJoinCode;
    public static bool IsSingleplayer => _instance && _instance.GetComponents<SingleplayerBehaviour>().Length > 0;
    public static bool IsServer => _instance && _instance.GetComponents<PingServerBehaviour>().Length > 0;
    public static bool IsClient => _instance && _instance.GetComponents<PingClientBehaviour>().Length > 0;
    public static event Action OnLobbyJoinStart;

    private IEnumerator Start()
    {
        _instance = this;
        
        yield return new WaitUntil(() => Game.ConstructorReady);
        if (gameObject.GetComponents<GameHostBehaviour>().Length > 0)
        {
            Debug.Log($"Game already launched, likely joined lobby.");
            yield break;
        }
        StartSingleplayer(new Game(true));
    }

    public static void StartSingleplayer(Game game)
    {
        var oldSingleplayers = _instance.GetComponents<SingleplayerBehaviour>();
        foreach (var singleplayer in oldSingleplayers)
            Destroy(singleplayer);
            
        var newSingleplayer = _instance.gameObject.AddComponent<SingleplayerBehaviour>();
        newSingleplayer.m_Game = game;
        Game.ClientGame = game;
    }
    
    public static void JoinLobby(string lobbyCode)
    {
        LastJoinCode = lobbyCode;
        OnLobbyJoinStart?.Invoke();
        _instance._JoinLobby(lobbyCode);
    }
    
    [EditorButton]
    void _JoinLobby(string lobbyCode)
    {
        // Start a new game, attempting to load data from the lobby code
        var newClient = gameObject.AddComponent<PingClientBehaviour>();
        newClient.StartCoroutine(newClient.Connect(lobbyCode, 
        OnSuccess: () =>
        {
            // Destroy any existing games
            foreach (var gameBehaviour in gameObject.GetComponents<GameHostBehaviour>())
            {
                if (gameBehaviour != newClient) Destroy((MonoBehaviour)gameBehaviour);
            }
            Game.ClientGame = newClient.m_Game;
        },
        OnFailure: () =>
        {
            Debug.LogError($"Failed to join lobby {lobbyCode}");
            Destroy(newClient);
            
            if (gameObject.GetComponents<GameHostBehaviour>().Length == 0)
            {
                Debug.LogWarning($"No game running, launching singleplayer game as fallback...");
                var singleplayer = gameObject.AddComponent<SingleplayerBehaviour>();
                singleplayer.m_Game = new Game(true);
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
