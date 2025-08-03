using UnityEngine;
using UnityEngine.SceneManagement;

public class GameInitialize : MonoBehaviour
{
    public static bool EnableMainContentLoad = true;
    public static bool EnableInitGameLaunch = true;

    private void Awake()
    {
        GameEvents.Initialize();
        
        var mainContentScene = SceneManager.GetSceneByName("main");
        if (EnableMainContentLoad && !mainContentScene.isLoaded)
        {
            SceneManager.LoadSceneAsync(mainContentScene.buildIndex, LoadSceneMode.Additive);
        }
        if (EnableInitGameLaunch)
        {
            GameLaunch.Create(new GameFactory("main"));
        }
    }

    private void OnDestroy()
    {
        GameEvents.Dispose();
    }
}