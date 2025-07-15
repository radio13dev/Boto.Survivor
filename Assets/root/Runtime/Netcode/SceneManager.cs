using System;
using Unity.Entities.Serialization;
using Unity.Scenes;
using UnityEngine;

public class SceneManager : MonoBehaviour
{
    static SceneManager m_Instance;
    
    public static bool Ready => m_Instance;
    public static SubScene GameManagerScene => m_Instance.gameManagerScene;
    public static SubScene GameScene => m_Instance.gameScene;
    
    public SubScene gameManagerScene;
    public SubScene gameScene;

    private void Start()
    {
        m_Instance = this;
    }

    private void OnDestroy()
    {
        if (m_Instance == this) m_Instance = null;
    }
}