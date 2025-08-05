using UnityEngine;
using UnityEngine.SceneManagement;

public class GameInitialize : MonoBehaviour
{
    public static bool EnableMainContentLoad = true;
    public static bool EnableInitGameLaunch = true;
    
    public static InputSystem_Actions Inputs;

    private void Awake()
    {
        GameEvents.Initialize();
        
        Inputs = new InputSystem_Actions();
        Inputs.Player.Enable();
        
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
        Inputs.Dispose();
        GameEvents.Dispose();
    }
}