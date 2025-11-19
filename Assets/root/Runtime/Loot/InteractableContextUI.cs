using System;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.EventSystems;

public class InteractableContextUI : MonoBehaviour, HandUIController.IStateSource
{
    public static InteractableContextUI Instance;
    public HandUIController.State GetUIState() => HandUIController.State.Inventory;

    private void Awake()
    {
        Instance = this;
    }

    public void Interact()
    {
        var e = GetComponentInParent<ShowInInteractRangeUI>().m_Entity;
        if (e == Entity.Null) return;
        
        if (GameEvents.HasComponent<LevelUpInteractableTag>(e))
        {
            // Open the LevelUp UI
            gameObject.SetActive(false);
            TiledStatsUI_InWorldTorus.OpenUI(DoOnceOnClose: () => gameObject.SetActive(true));
            return;
        }
        
        if (GameEvents.TryGetComponent2<LootDropInteractable>(e, out var lootDropInteractable))
        {
            // Open the LevelUp UI
            gameObject.SetActive(false);
            LootRewardUI.OpenUI(e, lootDropInteractable, DoOnceOnClose: () => gameObject.SetActive(true));
            return;
        }
        
        if (!GameEvents.TryGetComponent2<LocalTransform>(e, out var ringT)) return;
        if (!GameEvents.TryGetComponent2<RingStats>(e, out var ring)) return;
        if (!GameEvents.TryGetComponent2<CompiledStats>(CameraTarget.MainTarget.Entity, out var stats)) return;
        
        ChoiceUI.OnActiveRingChange += _Attach;
        ChoiceUI.Instance.Setup(stats, new Ring(){ Stats = ring }, ringT.Position);
    }
    
    // Delays attaching so the setup doesn't immediately reset
    void _Attach(int _)
    {
        ChoiceUI.OnActiveRingChange -= _Attach;
        
        gameObject.SetActive(false);
        HandUIController.SetState(HandUIController.State.Inventory);
        HandUIController.AddStateLayer(this); // Makes the UI ALWAYS show the inventory until we detach
        ChoiceUI.OnActiveRingChange += _Reset;
    }
    
    // Resets the UI when it's either closed OR another ring is slotted in.
    void _Reset(int _)
    {
        gameObject.SetActive(true);
        ChoiceUI.OnActiveRingChange -= _Reset;
        HandUIController.RemoveStateLayer(this);
    }
}
