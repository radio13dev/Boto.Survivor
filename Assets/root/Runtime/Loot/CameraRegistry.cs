using UnityEngine;

public class CameraRegistry : MonoBehaviour
{
    public static Camera Main { get; private set; }
    public static Camera UI { get; private set; }
    
    public bool IsMain;
    public bool IsUI;
    
    public static int UILayer;

    private void OnEnable()
    {
        UILayer = LayerMask.NameToLayer("UI");
        if (IsMain) Main = GetComponent<Camera>();
        if (IsUI) UI = GetComponent<Camera>();
    }

    private void OnDisable()
    {
        if (IsMain && Main && Main.gameObject == gameObject) Main = null;
        if (IsUI && UI && UI.gameObject == gameObject) UI = null;
    }
}