using System;
using Unity.Entities.Serialization;
using UnityEngine;

public class SceneManager : MonoBehaviour
{
    static SceneManager m_Instance;
    public static EntitySceneReference GameManagerScene => m_Instance.gameManagerScene;
    public static EntitySceneReference GameScene => m_Instance.gameScene;
    
    public EntitySceneReference gameManagerScene;
    public EntitySceneReference gameScene;

    private void Start()
    {
        m_Instance = this;
    }

    private void OnDestroy()
    {
        if (m_Instance == this) m_Instance = null;
    }
}