using System;
using Unity.Entities;
using UnityEngine;

public class HeartUI : MonoBehaviour, HandUIController.IStateChangeListener
{
    public TransitionPoint InventoryT;
    public TransitionPoint ClosedT;
    ExclusiveCoroutine Co;
    
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
    
    public void OnStateChanged(HandUIController.State oldState, HandUIController.State newState)
    {
        TransitionPoint target;
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

        Co.StartCoroutine(this, target.Lerp((RectTransform)transform, HandUIController.k_AnimTransitionTime));
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