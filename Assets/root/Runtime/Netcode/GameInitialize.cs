using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameInitialize : MonoBehaviour
{
    public static bool EnableMainContentLoad = true;
    public static bool EnableInitGameLaunch = true;

    public GameDebug DebugAsset;

    private void Awake()
    {
        if (DebugAsset) GameDebug.Initialize(DebugAsset);
        TiledStatsFull.Setup();
        GameEvents.Initialize();

        GameInput.Init();

        if (EnableMainContentLoad && !SceneManager.GetSceneByName("main").isLoaded)
        {
            SceneManager.LoadSceneAsync("main", LoadSceneMode.Additive);
        }

        if (EnableInitGameLaunch && !GameLaunch.Main)
        {
            GameLaunch.Create(new GameFactory("main"));
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
        GameInput.SetInputMode(arr[(Array.IndexOf(arr, GameInput.CurrentMode)+1)%arr.Length]);
    }
}