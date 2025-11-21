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
        GameEvents.OnVisualsUpdated += OnVisualsUpdated;
    }

    private void OnDisable()
    {
        GameEvents.OnVisualsUpdated -= OnVisualsUpdated;
    }
    private void OnVisualsUpdated(Entity entity)
    {
        if (entity != Entity) return;
        
        OnSetLink();
    }
    
    public override void OnSetLink()
    {
        base.OnSetLink();
        
        if (!Game.World.EntityManager.HasComponent<RingStats>(Entity)) return;
        
        var ringStats = Game.World.EntityManager.GetComponentData<RingStats>(Entity);
        RingDisplay.UpdateRing(-1, new Ring(){ Stats = ringStats }, true);
    }
}