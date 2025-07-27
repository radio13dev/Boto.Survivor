using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FingerUI : Selectable, IPointerClickHandler, ISubmitHandler, ICancelHandler, IPointerEnterHandler, IPointerExitHandler, HandUIController.IStateChangeListener, HandUIController.ILastPressListener
{
    public static FingerUI[] Instances = Array.Empty<FingerUI>();
    
    public int FingerIndex;
    public RingUIElement Ring;
    public GameObject FingerHighlight;
    public GameObject SelectHighlight;

    protected override void Awake()
    {
        base.Awake();
        OnDeselect(default);
        if (FingerIndex >= Instances.Length)
            Array.Resize(ref Instances, FingerIndex+1);
        Instances[FingerIndex] = this;
    }
    
    
    protected override void OnEnable()
    {
        base.OnEnable();
        HandUIController.Attach(this);
        
        Game.OnClientGameStarted += OnGameStarted;
        if (Game.ClientGame != null) OnGameStarted(Game.ClientGame);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        HandUIController.Detach(this);
        
        Game.OnClientGameStarted -= OnGameStarted;
        if (Game.ClientGame != null) Game.ClientGame.OnInventoryUpdated -= OnInventoryChanged;
    }

    private void OnGameStarted(Game game)
    {
        game.OnInventoryUpdated += OnInventoryChanged;
    }

    private void OnInventoryChanged(PlayerDataCache player)
    {
        if (player.PlayerIndex != Game.ClientGame.PlayerIndex) return;
        var inventory = player.Rings;
        Ring.gameObject.SetActive(inventory[FingerIndex].Stats.PrimaryEffect != RingPrimaryEffect.None);
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

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        if (FingerHighlight) FingerHighlight.gameObject.SetActive(true);
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        if (FingerHighlight) FingerHighlight.gameObject.SetActive(false);
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
            Game.ClientGame.RpcSendBuffer.Enqueue(SpecialLockstepActions.Rpc_PlayerAdjustInventory(Game.ClientGame.PlayerIndex, otherfinger.FingerIndex, this.FingerIndex));
            HandUIController.LastPressed = null;
        }
        else if (HandUIController.LastPressed is RingPopup ringPopup)
        {
            // Perform a swap + deselect both
            Game.ClientGame.RpcSendBuffer.Enqueue(SpecialLockstepActions.Rpc_PlayerAdjustInventory(Game.ClientGame.PlayerIndex, byte.MaxValue, this.FingerIndex));
            HandUIController.LastPressed = null;
        }
        else if (HandUIController.LastPressed is LootPopupOption lootOption)
        {
            // Perform a swap + deselect both
            Game.ClientGame.RpcSendBuffer.Enqueue(SpecialLockstepActions.Rpc_PlayerAdjustInventory(Game.ClientGame.PlayerIndex, Rpc_PlayerAdjustInventory.GetFloorIndexByte(lootOption.OptionIndex), this.FingerIndex));
            HandUIController.LastPressed = null;
        }
        else
        {
            HandUIController.LastPressed = this;
        }
    }

    public void OnLastPressChanged(Selectable oldPressed, Selectable newPressed)
    {
        if (SelectHighlight) SelectHighlight.SetActive(newPressed == this);
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
        if (newState != HandUIController.State.Inventory)
        {
            if (HandUIController.LastPressed == this)
            {
                if (newState == HandUIController.State.Neutral)
                {
                    // Do the 'drop item' transaction
                    Game.ClientGame.RpcSendBuffer.Enqueue(SpecialLockstepActions.Rpc_PlayerAdjustInventory(Game.ClientGame.PlayerIndex, this.FingerIndex, byte.MaxValue));
                }
                HandUIController.LastPressed = null;
            }
        }
    }

    private void OnStatsUpdated(Entity updated, CompiledStats stats, DynamicBuffer<Ring> rings)
    {
        Ring.gameObject.SetActive(rings[FingerIndex].Stats.PrimaryEffect != RingPrimaryEffect.None);
    }
}