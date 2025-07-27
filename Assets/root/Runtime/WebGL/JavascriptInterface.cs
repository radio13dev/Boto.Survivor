using UnityEngine;

public class JavascriptHook : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
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
        Object.FindAnyObjectByType<PingUIBehaviour>().StartLobbyJoinCo(lobbyCode);
    }
    
    [DllImport("__Internal")]
    public static extern void SetUrlArg(string key, string value);
#else
    public static void SetUrlArg(string lobby, string result) {}
#endif
}