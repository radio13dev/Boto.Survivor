using System.Runtime.InteropServices;
using UnityEngine;

public class JavascriptHook : MonoBehaviour
{
#if UNITY_WEBGL
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
#endif
}