using System.Linq;
using UnityEngine;

public class CameraRegistry : MonoBehaviour
{
    public static Camera Main
    {
        get
        {
            if (m_Main) return m_Main;
#if UNITY_EDITOR
            if (!Application.isPlaying) 
                return Camera.main;
#endif
            return null;
        }
        private set => m_Main = value;
    }
    static Camera m_Main;

    public static Camera UI
    {
        get
        {
            if (m_UI) return m_UI;
#if UNITY_EDITOR
            if (!Application.isPlaying) 
                return Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c => c.gameObject.name == "UI Camera");
#endif
            return null;
        }
        private set => m_UI = value;
    }

    static Camera m_UI;

    public static Camera Map
    {
        get
        {
            if (m_Map) return m_Map;
#if UNITY_EDITOR
            if (!Application.isPlaying) 
                return Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c => c.gameObject.name == "Map Camera");
#endif
            return null;
        }
        private set => m_Map = value;
    }

    static Camera m_Map;
    
    public bool IsMain;
    public bool IsUI;
    public bool IsMap;
    
    public static int UILayer;

    private void OnEnable()
    {
        UILayer = LayerMask.NameToLayer("UI");
        if (IsMain) Main = GetComponent<Camera>();
        if (IsUI) UI = GetComponent<Camera>();
        if (IsMap) Map = GetComponent<Camera>();
    }

    private void OnDisable()
    {
        if (IsMain && Main && Main.gameObject == gameObject) Main = null;
        if (IsUI && UI && UI.gameObject == gameObject) UI = null;
        if (IsMap && Map && Map.gameObject == gameObject) Map = null;
    }
}