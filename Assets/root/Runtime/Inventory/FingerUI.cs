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
        
        GameEvents.OnEvent += OnGameEvent;
        if (CameraTarget.MainTarget) OnGameEvent(GameEvents.Type.InventoryChanged, CameraTarget.MainTarget.Entity);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        HandUIController.Detach(this);
        
        GameEvents.OnEvent -= OnGameEvent;
    }

    private void OnGameEvent(GameEvents.Type eType, Entity entity)
    {
        if (eType != GameEvents.Type.InventoryChanged) return;
        if (!GameEvents.TryGetComponent<PlayerControlled>(entity, out var player)) return;
        if (player.Index != Game.ClientGame.PlayerIndex) return;
        if (!GameEvents.TryGetBuffer<Ring>(entity, out var rings)) return;
        OnInventoryChanged(rings);
    }

    private void OnInventoryChanged(DynamicBuffer<Ring> rings)
    {
        Ring.gameObject.SetActive(rings[FingerIndex].Stats.PrimaryEffect != RingPrimaryEffect.None);
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
            Game.ClientGame.RpcSendBuffer.Enqueue(GameRpc.PlayerAdjustInventory((byte)Game.ClientGame.PlayerIndex, (byte)otherfinger.FingerIndex, (byte)this.FingerIndex));
            HandUIController.LastPressed = null;
        }
        else if (HandUIController.LastPressed is RingPopup ringPopup)
        {
            // Perform a swap + deselect both
            Game.ClientGame.RpcSendBuffer.Enqueue(GameRpc.PlayerAdjustInventory((byte)Game.ClientGame.PlayerIndex, byte.MaxValue, (byte)this.FingerIndex, ringPopup.ItemPosition));
            HandUIController.LastPressed = null;
            HandUIController.SetState(HandUIController.State.Closed);
        }
        else if (HandUIController.LastPressed is LootPopupOption lootOption)
        {
            // Perform a swap + deselect both
            Game.ClientGame.RpcSendBuffer.Enqueue(GameRpc.PlayerAdjustInventory((byte)Game.ClientGame.PlayerIndex, GameRpc.GetFloorIndexByte(lootOption.OptionIndex), (byte)this.FingerIndex, lootOption.ItemPosition));
            HandUIController.LastPressed = null;
            HandUIController.SetState(HandUIController.State.Closed);
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
                    Game.ClientGame.RpcSendBuffer.Enqueue(GameRpc.PlayerAdjustInventory((byte)Game.ClientGame.PlayerIndex, (byte)this.FingerIndex, byte.MaxValue));
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