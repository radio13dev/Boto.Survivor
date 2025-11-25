using System;
using System.Reflection;
using BovineLabs.Saving;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using Collider = Collisions.Collider;
using Debug = UnityEngine.Debug;

public class LobbyStartButton : MonoBehaviour
{
    public GameObject StartButton;
    public GameObject TogglePublicButton; // Toggles between public and private lobby
    public TMP_Text TogglePublicButtonText;
    
    bool m_ActivatingLobby;

    private void Update()
    {
        // If we're in range of a lobby, hide buttons
        StartButton.SetActive(LobbyPresentationSystem.CurrentLobbyEntity == Entity.Null);
        TogglePublicButton.SetActive(LobbyPresentationSystem.CurrentLobbyEntity != Entity.Null && LobbyPresentationSystem.CurrentLobby.Owner == Game.ClientGame.PlayerIndex);
        if (TogglePublicButton.activeSelf)
        {
            var lobby = GameEvents.GetComponent<LobbyZone>(LobbyPresentationSystem.CurrentLobbyEntity);
            TogglePublicButtonText.text = lobby.IsPrivate ? "Private" : "Public";
        }
        if (!m_ActivatingLobby && LobbyPresentationSystem.CurrentLobbyEntity != Entity.Null && LobbyPresentationSystem.CurrentLobby.IsReady)
        {
            if (LobbyPresentationSystem.CurrentLobby.Owner == Game.ClientGame.PlayerIndex)
            {
                // Host the lobby
                m_ActivatingLobby = true;
                
                var oldGame = GameLaunch.Main;
                var newGame = GameLaunch.Create(new GameFactory("game", true));
                newGame.StartCoroutine(newGame.CreateServer(oldGame.Client.Lobby.Id + LobbyPresentationSystem.CurrentLobby.Owner.ToString(),
                    OnSuccess: () =>
                    {
                        GameLaunch.Main = newGame;
                        Destroy(oldGame.gameObject);
                        m_ActivatingLobby = false;
                    },
                    OnFailure: () =>
                    {
                        Destroy(newGame.gameObject);
                        m_ActivatingLobby = false;
                    })
                );
            }
            else
            {
                // Start the lobby 'join' process
                m_ActivatingLobby = true;
                
                var oldGame = GameLaunch.Main;
                var newGame = GameLaunch.Create(new GameFactory("game", true));
                
                newGame.StartCoroutine(newGame.JoinGame(
                    GameSearchKey: oldGame.Client.Lobby.Id + LobbyPresentationSystem.CurrentLobby.Owner.ToString(), 
                    TimeoutSeconds: 10,
                    OnSuccess: () =>
                    {
                        GameLaunch.Main = newGame;
                        Destroy(oldGame.gameObject);
                        m_ActivatingLobby = false;
                    },
                    OnFailure: () =>
                    {
                        Destroy(newGame.gameObject);
                        m_ActivatingLobby = false;
                    }));
            }
        }
    }

    public void OpenLobby()
    {
        var playerPos = CameraTarget.MainTarget.transform.position;
        Game.ClientGame.RpcSendBuffer.Enqueue(GameRpc.PlayerOpenLobby((byte)Game.ClientGame.PlayerIndex, playerPos, false));   
    }
    
    public void ToggleLobbyPublic()
    {
        var lobby = GameEvents.GetComponent<LobbyZone>(LobbyPresentationSystem.CurrentLobbyEntity);
        Game.ClientGame.RpcSendBuffer.Enqueue(GameRpc.PlayerSetLobbyPrivate((byte)Game.ClientGame.PlayerIndex, !lobby.IsPrivate));   
    }
}