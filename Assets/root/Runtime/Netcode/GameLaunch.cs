using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public abstract class GameHostBehaviour : MonoBehaviour
{
    public Game Game { get; set; }
    public abstract bool Idle { get; }
    public bool WaitingForStep => Game != null && !Game.CanStep();
}

public class GameLaunch : MonoBehaviour
{
    public static GameLaunch Main;

    public bool Idle => Game.ConstructorReady && GetComponents<GameHostBehaviour>().All(b => b.Idle);
    public bool IsSingleplayer => GetComponent<SingleplayerBehaviour>() is { } singleplayer && singleplayer && singleplayer.Idle;
    public bool IsClient => GetComponent<PingClientBehaviour>() is { } client && client && client.Idle;
    public bool IsServer => GetComponent<PingServerBehaviour>() is { } server && server && server.Idle;

    public SingleplayerBehaviour Singleplayer => GetComponent<SingleplayerBehaviour>();
    public PingClientBehaviour Client => GetComponent<PingClientBehaviour>();
    public PingServerBehaviour Server => GetComponent<PingServerBehaviour>();

    public bool Initialized;
    IGameFactory m_GameFactory;

    public static GameLaunch Create(IGameFactory factory)
    {
        var gameLaunch = new GameObject(nameof(GameLaunch), typeof(GameLaunch)).GetComponent<GameLaunch>();
        gameLaunch.m_GameFactory = factory;
        return gameLaunch;
    }

    private IEnumerator Start()
    {
        Main = this;

        if (!Idle) yield return new WaitUntil(() => Idle);
        if (gameObject.GetComponents<GameHostBehaviour>().Length > 0)
        {
            Debug.Log($"Game already launched, likely joined lobby.");
            Initialized = true;
            yield break;
        }

        yield return StartSingleplayer(m_GameFactory.Invoke());
        Initialized = true;
    }

    public IEnumerator StartSingleplayer(Game game)
    {
        if (!Idle) yield return new WaitUntil(() => Idle);

        var oldSingleplayers = GetComponents<SingleplayerBehaviour>();
        foreach (var singleplayer in oldSingleplayers)
            Destroy(singleplayer);

        var newSingleplayer = gameObject.AddComponent<SingleplayerBehaviour>();
        newSingleplayer.Game = game;
        Game.ClientGame = game;

        if (!Idle) yield return new WaitUntil(() => Idle);
    }

    public IEnumerator JoinLobby(string lobbyCode)
    {
        if (!Idle) yield return new WaitUntil(() => Idle);

        // Start a new game, attempting to load data from the lobby code
        var newClient = gameObject.AddComponent<PingClientBehaviour>();
        newClient.StartCoroutine(
            newClient.Connect(m_GameFactory, lobbyCode,
                // Called after the game is downloaded from the server
                OnGameLoad: () =>
                {
                    // Make the new game active
                    Game.ClientGame = newClient.Game;

                    // Destroy any existing games
                    foreach (var gameBehaviour in gameObject.GetComponents<GameHostBehaviour>())
                    {
                        if (gameBehaviour == newClient) continue;
                        if (gameBehaviour is PingServerBehaviour server && server.JoinCode == lobbyCode) continue;
                        Destroy((MonoBehaviour)gameBehaviour);
                    }
                },
                // Called if a failure occurs before the game has loaded
                OnFailureBeforeLoad: () =>
                {
                    Destroy(newClient);

                    if (!Singleplayer)
                    {
                        Debug.LogWarning($"No game running, launching singleplayer game as fallback...");
                        StartCoroutine(StartSingleplayer(m_GameFactory.Invoke()));
                    }
                },
                // Called if a failure occurs after the game has loaded
                OnFailureAfterLoad: () =>
                {
                    var failedGame = newClient.Game;
                    newClient.Game = null; // Prevents the game from being destroyed
                    failedGame.CleanForSingleplayer();
                    StartCoroutine(StartSingleplayer(failedGame));
                })
        );
        
        if (!Idle) yield return new WaitUntil(() => Idle);
    }

    public IEnumerator CreateServer()
    {
        if (!Idle) yield return new WaitUntil(() => Idle);

        if (!Singleplayer)
            yield return StartSingleplayer(m_GameFactory.Invoke());
            
        var singleplayer = Singleplayer;
        var server = gameObject.AddComponent<PingServerBehaviour>();
        server.Game = Singleplayer.Game;
        server.StartCoroutine(server.Connect(
            OnSuccess: () =>
            {
                server.AddLocalPlayer();
                singleplayer.Game = null; // Null out our game so we don't destroy the server when we destroy this component
                Destroy(singleplayer);
                Game.ClientGame = server.Game;
            },
            OnFailure: () =>
            {
                Debug.LogError($"Failed to start server");
                server.Game = null;
                Destroy(server);
            })
        );
        
        if (!Idle) yield return new WaitUntil(() => Idle);
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

    public IEnumerator Disconnect()
    {
        if (!Idle) yield return new WaitUntil(() => Idle);

        Game singleplayerGame = null;
        if (IsClient)
        {
            singleplayerGame?.Dispose();
        
            // Destroy client, use that world for our singleplayer game
            var client = Client;
            singleplayerGame = client.Game ?? singleplayerGame;
            client.Game = null;
            Destroy(client);
            singleplayerGame?.CleanForSingleplayer();
        }

        if (IsServer)
        {
            singleplayerGame?.Dispose();
        
            // Destroy server, use that world for our singleplayer game
            var server = Server;
            singleplayerGame = server.Game ?? singleplayerGame;
            server.Game = null;
            Destroy(server);
            singleplayerGame?.CleanForSingleplayer();
        }
        
        if (singleplayerGame == null) 
        {
            // No game to clean, just start a new singleplayer game
            singleplayerGame = m_GameFactory.Invoke();
        }
        
        yield return StartSingleplayer(singleplayerGame);
    }
}