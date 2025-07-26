using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FingerUI : Selectable, IPointerClickHandler, ISubmitHandler, ICancelHandler, HandUIController.IStateChangeListener
{
    public static FingerUI[] Instances = Array.Empty<FingerUI>();
    
    public int FingerIndex;
    public RingUIElement Ring;
    public GameObject FingerHighlight;
    
    GameAttachmentDescription m_AttachmentDescription;

    protected override void Awake()
    {
        base.Awake();
        OnDeselect(default);
        if (FingerIndex >= Instances.Length)
            Array.Resize(ref Instances, FingerIndex+1);
        Instances[FingerIndex] = this;
        
        m_AttachmentDescription = new GameAttachmentDescription(
            gameType: GameType.Presentation,
            onInventoryChanged: (-1, OnInventoryChanged)
        );
    }
    
    
    protected override void OnEnable()
    {
        base.OnEnable();
        HandUIController.Attach(this);
        CompiledStatsDirty.OnStatsUpdated += OnStatsUpdated;
        
        m_AttachmentDescription?.Attach();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        HandUIController.Detach(this);
        CompiledStatsDirty.OnStatsUpdated -= OnStatsUpdated;
        
        m_AttachmentDescription?.Detach();
    }
    
    private void OnInventoryChanged(Ring[] rings)
    {
        
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        HandUIController.SetState(HandUIController.State.Inventory);
        if (FingerHighlight) FingerHighlight.gameObject.SetActive(true);
    }

    public override void OnDeselect(BaseEventData eventData)
    {
        base.OnDeselect(eventData);
        if (FingerHighlight) FingerHighlight.gameObject.SetActive(false);
    }
    
    public void Refresh()
    {
        var inventory = Game.PresentationGame.GetInventory(Game.PresentationGame.PlayerIndex);
        Ring.gameObject.SetActive(inventory[FingerIndex].Stats.PrimaryEffect != RingPrimaryEffect.None);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnSubmit(eventData);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (HandUIController.LastPressed == this)
        {
            HandUIController.LastPressed = null;
        }
        else if (HandUIController.LastPressed is FingerUI otherfinger)
        {
            // Perform a swap + deselect both
            Game.PresentationGame.RpcSendBuffer.Enqueue(SpecialLockstepActions.Rpc_PlayerAdjustInventory(Game.PresentationGame.PlayerIndex, otherfinger.FingerIndex, this.FingerIndex));
            HandUIController.LastPressed = null;
        }
        else if (HandUIController.LastPressed is RingPopup ringPopup)
        {
            // Perform a swap + deselect both
            Game.PresentationGame.RpcSendBuffer.Enqueue(SpecialLockstepActions.Rpc_PlayerAdjustInventory(Game.PresentationGame.PlayerIndex, byte.MaxValue, this.FingerIndex));
            HandUIController.LastPressed = null;
        }
        else
        {
            HandUIController.LastPressed = this;
        }
    }

    public void OnCancel(BaseEventData eventData)
    {
        if (HandUIController.LastPressed == this)
            HandUIController.LastPressed = null;
        else 
            HandUIController.SetState(HandUIController.State.Closed);
    }

    public void OnStateChanged(HandUIController.State oldState, HandUIController.State newState)
    {
        if (newState == HandUIController.State.Closed) this.Deselect();
    }

    private void OnStatsUpdated(Entity updated, CompiledStats stats, DynamicBuffer<Ring> rings)
    {
        Ring.gameObject.SetActive(rings[FingerIndex].Stats.PrimaryEffect != RingPrimaryEffect.None);
    }
}