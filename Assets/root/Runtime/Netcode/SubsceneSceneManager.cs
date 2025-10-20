using Unity.Scenes;
using UnityEngine;

public class SubsceneSceneManager : MonoBehaviour
{
    static SubsceneSceneManager m_Instance;
    
    public static bool Ready => m_Instance;
    public static SubScene GameManagerScene => m_Instance.gameManagerScene;
    public static SubScene[] GameScenes => m_Instance.gameScenes;
    
    [Header("All game types use the same manager")]
    public SubScene gameManagerScene;
    
    [Header("Lobby: 0, Game: 1")]
    public SubScene[] gameScenes;

    private void Start()
    {
        m_Instance = this;
    }

    private void OnDestroy()
    {
        if (m_Instance == this) m_Instance = null;
    }
}