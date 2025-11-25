using Unity.Entities;
using UnityEngine;

public class LobbyPlayerInteractButton : MonoBehaviour
{
    /// <summary>
    /// This Visual will most likely have a Key Press Listener on it.
    /// </summary>
    public GameObject Visual;
    public LobbyPlayerInteractWindow LobbyPlayerInteractWindow;
    public PlayerTitle InteractWindowHeader;

    private void Update()
    {
        bool show = LobbyPlayerInteractionSystem.NearestPlayer != Entity.Null && !LobbyPlayerInteractWindow.gameObject.activeSelf;
        Visual.SetActive(show);
    }
    
    public void OnClick()
    {
        LobbyPlayerInteractWindow.gameObject.SetActive(true);
        LobbyPlayerInteractWindow.Setup(LobbyPlayerInteractionSystem.NearestPlayer);
        InteractWindowHeader.Setup(LobbyPlayerInteractionSystem.NearestPlayer);
    }
}