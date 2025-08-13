using System;
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
        Inputs.UI.Enable();
        
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
        Inputs.Dispose();
        GameEvents.Dispose();
    }
}