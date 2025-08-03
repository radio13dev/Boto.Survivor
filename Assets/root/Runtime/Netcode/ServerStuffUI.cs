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
        PingClientBehaviour.OnLobbyJoinStart += RefreshLobbyCodeDisplay;
        PingServerBehaviour.OnLobbyHostStart += RefreshLobbyCodeDisplay;
        RefreshLobbyCodeDisplay();
    }

    private void OnDisable()
    {
        PingClientBehaviour.OnLobbyJoinStart -= RefreshLobbyCodeDisplay;
        PingServerBehaviour.OnLobbyHostStart -= RefreshLobbyCodeDisplay;
    }

    private void Update()
    {
        if (!GameLaunch.Main) return;
        
        HostLobbyButton.SetActive(GameLaunch.Main.IsSingleplayer);
        ConnectButton.SetActive(GameLaunch.Main.IsSingleplayer);
        DisconnectButton.SetActive(GameLaunch.Main.IsServer || GameLaunch.Main.IsClient);
        LobbyCodeButton.SetActive(GameLaunch.Main.IsServer || GameLaunch.Main.IsSingleplayer);
        ResyncButton.SetActive(GameLaunch.Main.IsClient);
    }

    public void OnHostLobbyButton()
    {
        GameLaunch.Main.StartCoroutine(GameLaunch.Main.CreateServer());
    }

    public void OnConnectButton()
    {
        GameLaunch.Main.StartCoroutine(GameLaunch.Main.JoinLobby(LobbyField.text));
    }

    public void OnDisconnectButton()
    {
        GameLaunch.Main.StartCoroutine(GameLaunch.Main.Disconnect());
    }

    public void OnResyncButton()
    {
        GameLaunch.Main.Client.RequestNewSave();
    }

    public void RefreshLobbyCodeDisplay()
    {
        if (GameLaunch.Main)
            LobbyCodeText.text = GameLaunch.Main.Server?.JoinCode ??
                                 GameLaunch.Main.Client?.JoinCode;
    }

    public void OnCopyLobbyCodeButton()
    {
        ClipboardBridge.CopyToClipboard(GameLaunch.Main.Server?.JoinCode);
        Debug.Log($"Copied lobby code: {LobbyField.text}");
    }
}