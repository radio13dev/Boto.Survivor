using System;
using UnityEngine;

public class ShowForGameMode : MonoBehaviour
{
    public int ShowForGameType;
    
    private void Awake()
    {
        Game.OnClientGameChanged += OnClientGameChanged;
        OnClientGameChanged();
    }

    private void OnDestroy()
    {
        Game.OnClientGameChanged -= OnClientGameChanged;
    }

    private void OnClientGameChanged()
    {
        if (Game.ClientGame == null || Game.ClientGame.GameType != ShowForGameType)
        {
            gameObject.SetActive(false);
        }
        else
        {
            gameObject.SetActive(true);
        }
    }
}
