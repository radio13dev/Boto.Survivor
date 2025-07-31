using UnityEngine;

public class ClipboardBridge : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void CopyToClipboardAndShare(string textToCopy);
#endif

    public static void CopyToClipboard(string text)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CopyToClipboardAndShare(text);
#else
        GUIUtility.systemCopyBuffer = text;
#endif
        Debug.Log("Copied text to clipboard!");
    }
}