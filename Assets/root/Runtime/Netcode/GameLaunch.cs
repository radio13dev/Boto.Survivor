using System;
using System.Collections;
using System.Collections.Generic;
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
    
    static Queue<Action> _actionQueue = new();

    private IEnumerator Start()
    {
        yield return new WaitUntil(() => Game.ConstructorReady);
        _instance = this;
        while (_actionQueue.Count > 0)
        {
            var action = _actionQueue.Dequeue();
            action?.Invoke();
        }
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
        Execute(i => i._JoinLobby(lobbyCode));
    }

    private static void Execute(Action<GameLaunch> func)
    {
        if (!_instance) _actionQueue.Enqueue(() => func(_instance));
        else func(_instance);
    }

    [EditorButton]
    void _JoinLobby(string lobbyCode)
    {
        OnLobbyJoinStart?.Invoke();
        // Start a new game, attempting to load data from the lobby code
        var newClient = gameObject.AddComponent<PingClientBehaviour>();
        newClient.StartCoroutine(newClient.Connect(lobbyCode, 
        OnSuccess: () =>
        {
            // Destroy any existing games
            foreach (var gameBehaviour in gameObject.GetComponents<GameHostBehaviour>())
            {
                if (gameBehaviour == newClient) continue;
                if (gameBehaviour is PingServerBehaviour server && server.JoinCode == lobbyCode) continue;
                Destroy((MonoBehaviour)gameBehaviour);
            }
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
