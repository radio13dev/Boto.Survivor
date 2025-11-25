using System;
using System.Linq;
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

public class LobbyStartButton2 : MonoBehaviour
{
    public GameObject StartButton;
    public GameObject TogglePublicButton; // Toggles between public and private lobby
    public TMP_Text TogglePublicButtonText;
    public GameObject LoadingIcon;
    
    bool m_ActivatingLobby;

    private void Update()
    {
        if (m_ActivatingLobby)
        {
            StartButton.SetActive(false);
            TogglePublicButton.SetActive(false);
            LoadingIcon.SetActive(true);
            return;
        }
    
        // If we're in range of a lobby, hide buttons
        LoadingIcon.SetActive(false);
        StartButton.SetActive(LobbyPresentationSystem.CurrentLobbyEntity == Entity.Null);
        TogglePublicButton.SetActive(LobbyPresentationSystem.CurrentLobbyEntity != Entity.Null && LobbyPresentationSystem.CurrentLobby.Owner == Game.ClientGame.PlayerIndex);
        if (TogglePublicButton.activeSelf)
        {
            var lobby = GameEvents.GetComponent<LobbyZone>(LobbyPresentationSystem.CurrentLobbyEntity);
            TogglePublicButtonText.text = lobby.IsPrivate ? "Private" : "Public";
        }
        if (LobbyPresentationSystem.CurrentLobbyEntity != Entity.Null && LobbyPresentationSystem.CurrentLobby.IsReady)
        {
            if (LobbyPresentationSystem.CurrentLobby.Owner == Game.ClientGame.PlayerIndex)
            {
                // Host the lobby
                m_ActivatingLobby = true;
                
                var oldGame = GameLaunch.Main;
                var newGame = GameLaunch.Create(new GameFactory("game"));
                newGame.StartCoroutine(newGame.CreateServer("game",
                    OnSuccess: () =>
                    {
                        Debug.Log($"Hosting successful, server created.");
                        GameLaunch.Main = newGame;
                        Destroy(oldGame.gameObject);
                        m_ActivatingLobby = false;
                    },
                    OnFailure: () =>
                    {
                        Debug.Log($"Hosting failed, staying in lobby.");
                        Destroy(newGame.gameObject);
                        m_ActivatingLobby = false;
                    })
                );
            }
            else
            {
                // Start the lobby 'join' process
                m_ActivatingLobby = true;
                
                var id = LobbySpawnerSingleplayer.m_ClientsideLobbyReferences.FirstOrDefault(kvp => kvp.Value.Item2 == LobbyPresentationSystem.CurrentLobbyEntity).Key;
                
                var oldGame = GameLaunch.Main;
                var newGame = GameLaunch.Create(new GameFactory("game"));
                
                newGame.StartCoroutine(newGame.JoinLobby2(id, 
                    OnSuccess: () =>
                    {
                        Debug.Log($"Join successful, destroying lobby.");
                        GameLaunch.Main = newGame;
                        Destroy(oldGame.gameObject);
                        m_ActivatingLobby = false;
                    },
                    OnFailure: () =>
                    {
                        Debug.Log($"Join failed, staying in lobby.");
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