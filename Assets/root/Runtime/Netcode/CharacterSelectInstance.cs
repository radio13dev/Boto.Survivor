using UnityEngine;

public class CharacterSelectInstance : MonoBehaviour
{
    public GameObject Display;
    
    private void Update()
    {
        Display.SetActive(Game.ClientGame != null && Game.ClientGame.PlayerIndex != -1 && !CameraTarget.MainTarget);
    }
    
    public void SelectCharacter(int index)
    {
        Game.ClientGame.RpcSendBuffer.Enqueue(GameRpc.PlayerJoin((byte)Game.ClientGame.PlayerIndex, (byte)index));
    }
}