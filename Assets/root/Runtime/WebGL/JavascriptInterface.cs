using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public class JavascriptHook : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    public static extern void SetUrlArg(string key, string value);
#else
    public static void SetUrlArg(string lobby, string result) {}
#endif

    public void WebGPUStatus(bool status)
    {
        WebGpu.Enabled.Data = status;
    }

    public void SetFocused(int focused)
    {
        // Nothing
    }

    public void JoinLobby(string lobbyCode)
    {
        if (!GameLaunch.Main) GameLaunch.Create(new GameFactory("main"));
        GameLaunch.Main.StartCoroutine(GameLaunch.Main.JoinRelay(lobbyCode));
    }
}