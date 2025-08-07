using System;
using System.Collections.Generic;
using BovineLabs.Core.Extensions;
using BovineLabs.Saving;
using Unity.Entities;
using UnityEngine;

public class InventoryUI : MonoBehaviour, HandUIController.IStateChangeListener
{
    public Transform ClosedT;
    public Transform InventoryT;
    ExclusiveCoroutine Co;
    
    Dictionary<uint, GemDisplay> m_InventoryGems = new();
    public GemDisplay GemDisplayPrefab;
    public RingDisplay[] RingDisplays;
    public RingFocusDisplay RingFocusDisplay;

    private void Awake()
    {
        GemDisplayPrefab.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        HandUIController.Attach(this);
        
        UIFocus.OnFocus += OnFocus;
        UIFocus.OnInteract += OnInteract;
        GameEvents.OnEvent += OnGameEvent;
        if (CameraTarget.MainTarget) OnGameEvent(GameEvents.Type.InventoryChanged, CameraTarget.MainTarget.Entity);
    }

    private void OnDisable()
    {
        HandUIController.Detach(this);
        
        UIFocus.OnFocus -= OnFocus;
        UIFocus.OnInteract -= OnInteract;
        GameEvents.OnEvent -= OnGameEvent;
    }

    private void OnFocus()
    {
        if (UIFocus.Focus && UIFocus.Focus.TryGetComponent<RingDisplay>(out var display))
        {
            var index = Array.IndexOf(RingDisplays, display);
            if (index != -1)
            {
                RingFocusDisplay.UpdateRing(index, display);
            }
        }
    }

    private void OnInteract()
    {
        
    }
    
    public void OnStateChanged(HandUIController.State oldState, HandUIController.State newState)
    {
        Transform target;
        switch (newState)
        {
            case HandUIController.State.Inventory:
                target = InventoryT;
                break;
            default:
            case HandUIController.State.Closed:
                target = ClosedT;
                break;
        }

        Co.StartCoroutine(this, CoroutineHost.Methods.LerpSmooth(transform, target, HandUIController.k_AnimTransitionTime));
    }

    private void OnGameEvent(GameEvents.Type eType, Entity entity)
    {
        if (eType != GameEvents.Type.InventoryChanged) return;
        if (!GameEvents.TryGetComponent<PlayerControlled>(entity, out var player)) return;
        if (player.Index != Game.ClientGame.PlayerIndex) return;
        if (GameEvents.TryGetBuffer<Ring>(entity, out var rings) && GameEvents.TryGetBuffer<EquippedGem>(entity, out var equippedGems))
        {
        
            // Update ring display
            for (int i = 0; i < rings.Length && i < RingDisplays.Length; i++)
            {
                // Get the 'subset' of gems that this ring uses
                var equippedGemsForRing = equippedGems.AsNativeArray().AsReadOnlySpan().Slice(i*Gem.k_GemsPerRing, Gem.k_GemsPerRing);
                RingDisplays[i].UpdateRing(rings[i], equippedGemsForRing);
                
                // Update the focus display if this ring is focused
                if (RingFocusDisplay.IsFocused(RingDisplays[i]) || RingFocusDisplay.IsFocused(null))
                    RingFocusDisplay.UpdateRing(i, RingDisplays[i]);
            }
        }
        if (GameEvents.TryGetBuffer<InventoryGem>(entity, out var gems))
        {
            // Update gem display
            HashSet<uint> toRemove = new(m_InventoryGems.Keys);
            for (int i = 0; i < gems.Length; i++)
            {
                toRemove.Remove(gems[i].Gem.ClientId);
                
                if (!m_InventoryGems.TryGetValue(gems[i].Gem.ClientId, out var gemDisplay))
                {
                    // Create a new display if it doesn't exist
                    gemDisplay = Instantiate(GemDisplayPrefab, GemDisplayPrefab.transform.parent);
                    m_InventoryGems[gems[i].Gem.ClientId] = gemDisplay;
                    gemDisplay.UpdateGem(i, gems[i].Gem);
                    gemDisplay.SnapBackToOrigin();
                    gemDisplay.gameObject.SetActive(true);
                }
                else
                {
                    // Old ones SHOULDN'T need updating
                }
            }
            // Remove any displays that are no longer in the inventory
            foreach (var remove in toRemove)
                if (m_InventoryGems.Remove(remove, out var gemDisplay))
                    Destroy(gemDisplay.gameObject);
        }
    }
}