using Unity.Entities;
using UnityEngine;

public class LobbyPlayerInteractWindow : MonoBehaviour
{
    Entity m_Player;

    // Invite to private lobby, Player details, Close window
    public void Setup(Entity player)
    {
        m_Player = player;
    }
    
    public void Close()
    {
        gameObject.SetActive(false);
    }
    
    public void InviteToPrivateLobby()
    {
        var playerPos = CameraTarget.MainTarget.transform.position;
        Game.ClientGame.RpcSendBuffer.Enqueue(
            GameRpc.PlayerOpenLobby((byte)Game.ClientGame.PlayerIndex, playerPos, true));
        Game.ClientGame.RpcSendBuffer.Enqueue(
            GameRpc.PlayerInviteToPrivateLobby((byte)Game.ClientGame.PlayerIndex, PlayerControlled.GetPlayerIndex(Game.ClientGame.World, m_Player)));
    }
}