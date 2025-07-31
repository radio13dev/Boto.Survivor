using System;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

public class ServerStuffUI : MonoBehaviour
{
    public GameObject HostLobbyButton;
    public GameObject ConnectButton;
    public TMP_InputField LobbyField;
    
    public GameObject DisconnectButton;
    public GameObject LobbyCodeButton;
    public TMP_Text LobbyCodeText;
    public GameObject ResyncButton;

    private void OnEnable()
    {
        GameLaunch.OnLobbyJoinStart += RefreshLobbyCodeDisplay;
        PingServerBehaviour.OnLobbyHostStart += RefreshLobbyCodeDisplay;
        RefreshLobbyCodeDisplay();
    }

    private void OnDisable()
    {
        GameLaunch.OnLobbyJoinStart -= RefreshLobbyCodeDisplay;
        PingServerBehaviour.OnLobbyHostStart -= RefreshLobbyCodeDisplay;
    }

    private void Update()
    {
        HostLobbyButton.SetActive(GameLaunch.IsSingleplayer);
        ConnectButton.SetActive(GameLaunch.IsSingleplayer);
        DisconnectButton.SetActive(GameLaunch.IsServer || GameLaunch.IsClient);
        LobbyCodeButton.SetActive(GameLaunch.IsServer || GameLaunch.IsSingleplayer);
        ResyncButton.SetActive(GameLaunch.IsClient);
    }
    
    public void OnHostLobbyButton()
    {
        var potentialClient = Object.FindAnyObjectByType<SingleplayerBehaviour>();
        if (potentialClient) potentialClient.Host();
    }
    
    public void OnConnectButton()
    {
        GameLaunch.JoinLobby(LobbyField.text);
    }
    
    public void OnDisconnectButton()
    {
        bool shouldLaunchSingleplayer = !GameLaunch.IsSingleplayer;
        if (shouldLaunchSingleplayer)
        {
            var potentialClient = Object.FindAnyObjectByType<PingClientBehaviour>();
            if (potentialClient)
            {
                potentialClient.m_Game.CleanForSingleplayer();
                GameLaunch.StartSingleplayer(potentialClient.m_Game);
                potentialClient.m_Game = null;
            }
            else
            {
                var potentialServer = Object.FindAnyObjectByType<PingServerBehaviour>();
                if (potentialServer)
                {
                    potentialServer.m_Game.CleanForSingleplayer();
                    GameLaunch.StartSingleplayer(potentialServer.m_Game);
                    potentialServer.m_Game = null;
                }
            }
        }
        
        var pingClients = Object.FindObjectsByType<PingClientBehaviour>(FindObjectsSortMode.None);
        foreach (var client in pingClients)
            Destroy(client);
            
        var pingServers = Object.FindObjectsByType<PingServerBehaviour>(FindObjectsSortMode.None);
        foreach (var server in pingServers)
            Destroy(server);
    }
    
    public void OnResyncButton()
    {
        var pingClients = Object.FindObjectsByType<PingClientBehaviour>(FindObjectsSortMode.None);
        foreach (var client in pingClients)
            client.RequestNewSave();
    }
    
    public void RefreshLobbyCodeDisplay()
    {
        LobbyField.text = GameLaunch.LastJoinCode;
        LobbyCodeText.text = Object.FindAnyObjectByType<PingServerBehaviour>()?.JoinCode;
    }
    
    public void OnCopyLobbyCodeButton()
    {
        ClipboardBridge.CopyToClipboard(Object.FindAnyObjectByType<PingServerBehaviour>()?.JoinCode ?? GameLaunch.LastJoinCode);
        Debug.Log($"Copied lobby code: {LobbyField.text}");
    }
}
