using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RingPopup : EntityLinkMono
{
    public RingDisplay RingDisplay;
    
    private void OnEnable()
    {
        GameEvents.OnEvent += OnGameEvent;
    }

    private void OnDisable()
    {
        GameEvents.OnEvent -= OnGameEvent;
    }
    private void OnGameEvent(GameEvents.Type eType, Entity entity)
    {
        if (eType != GameEvents.Type.VisualsUpdated) return;
        if (entity != Entity) return;
        
        OnSetLink();
    }
    
    public override void OnSetLink()
    {
        base.OnSetLink();
        
        if (!Game.World.EntityManager.HasComponent<RingStats>(Entity)) return;
        
        var ringStats = Game.World.EntityManager.GetComponentData<RingStats>(Entity);
        RingDisplay.UpdateRing(-1, new Ring(){ Stats = ringStats });
    }
}