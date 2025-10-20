using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class PlayerTitle : MonoBehaviour
{
    public TMP_Text Name;
    public TMP_Text Title;  // Customizable
    public Image Icon;      // Customizable
    Entity m_Player;
    
    public void Setup(Entity player)
    {
        m_Player = player;
    }
}