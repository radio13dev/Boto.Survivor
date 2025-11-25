using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public class GameInitialize : MonoBehaviour
{
    public static bool EnableMainContentLoad = true;

    public static eMode InitMode = eMode.Singleplayer;

    public enum eMode
    {
        None,
        Singleplayer,
        Lobby,
        Game
    }

    public GameDebug DebugAsset;

    private void Awake()
    {
        if (DebugAsset) GameDebug.Initialize(DebugAsset);
        TiledStatsFull.Setup();
        GameEvents.Initialize();

        GameInput.Init();
    }

    private IEnumerator Start()
    {
        yield return null;

        if (EnableMainContentLoad)
        {
            var mainScene = SceneManager.GetSceneByName("main");
            if (!mainScene.isLoaded)
                    SceneManager.LoadSceneAsync("main", LoadSceneMode.Additive);
        }
        else if (!Object.FindFirstObjectByType<AudioListener>())
            gameObject.AddComponent<AudioListener>();

        if (!GameLaunch.Main)
            switch (InitMode)
            {
                case eMode.None: break;
                case eMode.Singleplayer:
                    var singleplayerLobby = GameLaunch.Create(new LobbyFactory("main"));
                    GameLaunch.Main = singleplayerLobby;
                    break;
                case eMode.Lobby:
                    JoinFirstLobbyOrDefault();
                    break;
                case eMode.Game:
                    var singleplayerGame = GameLaunch.Create(new GameFactory("main"));
                    GameLaunch.Main = singleplayerGame;
                    break;
            }
        
    }

    private void Update()
    {
        GameEvents.ExecuteEvents();
    }

    private void OnDestroy()
    {
        GameInput.Dispose();
        GameEvents.Dispose();
        TiledStatsFull.Dispose();
    }

    [EditorButton]
    public void CycleInputMode()
    {
        var arr = ((GameInput.Mode[])Enum.GetValues(typeof(GameInput.Mode)));
        GameInput.SetInputMode(arr[(Array.IndexOf(arr, GameInput.CurrentMode) + 1) % arr.Length]);
    }

    [EditorButton]
    async void JoinFirstLobbyOrDefault()
    {
        var signInTask = GameLaunch.SignIn();
        await signInTask;

        var gameLaunch = GameLaunch.Create(new LobbyFactory("lobby"));
        GameLaunch.Main = gameLaunch;

        try
        {
            QueryResponse lobbies = await LobbyService.Instance.QueryLobbiesAsync();
            if (lobbies.Results.Count > 0)
            {
                var mthd = typeof(Lobby).GetMethod("SerializeAsPathParam", BindingFlags.Instance | BindingFlags.NonPublic);
                Debug.Log("Lobbies found:\n- " + string.Join("\n- ", lobbies.Results.Select(l => mthd.Invoke(l, Array.Empty<object>()))));
                gameLaunch.StartCoroutine(gameLaunch.JoinLobby2(lobbies.Results[0].Id,
                    OnSuccess: () => { },
                    OnFailure: () =>
                    {
                        Debug.Log("Failed lobby join, creating one...");
                        gameLaunch.StartCoroutine(gameLaunch.CreateServer());
                    }));
            }
            else
            {
                Debug.Log("No available lobbies found, creating one...");
                gameLaunch.StartCoroutine(gameLaunch.CreateServer());
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }
}