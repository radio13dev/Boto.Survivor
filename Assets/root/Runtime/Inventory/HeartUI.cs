using System;
using Unity.Entities;
using UnityEngine;

public class HeartUI : MonoBehaviour
{
    public Heart[] Hearts = Array.Empty<Heart>();
    
    private void OnEnable()
    {
        HandUIController.Attach(this);

        GameEvents.OnEvent += OnGameEvent;
        if (CameraTarget.MainTarget) OnGameEvent(new (GameEvents.Type.PlayerHealthChanged, CameraTarget.MainTarget.Entity));
    }

    private void OnDisable()
    {
        HandUIController.Detach(this);

        GameEvents.OnEvent -= OnGameEvent;
    }
    
    private void OnGameEvent(GameEvents.Data data)
    {
        var eType = data.Type; var entity = data.Entity;
        if (eType != GameEvents.Type.PlayerHealthChanged) return;
        if (!GameEvents.TryGetSharedComponent<PlayerControlled>(entity, out var player)) return;
        if (player.Index != Game.ClientGame.PlayerIndex) return;
        
        int health = 0;
        if (GameEvents.TryGetComponent2<Health>(entity, out var healthComp)) health = healthComp.Value;
        
        int overshield = health/Hearts.Length;
        for (int i = 0; i < Hearts.Length; i++)
        {
            Hearts[i].SetValue(overshield + (health%Hearts.Length > i ? 1 : 0));
        }
    }
}