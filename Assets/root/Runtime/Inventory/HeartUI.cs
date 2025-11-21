using System;
using Unity.Entities;
using UnityEngine;

public class HeartUI : MonoBehaviour
{
    public Heart[] Hearts = Array.Empty<Heart>();
    
    private void OnEnable()
    {
        HandUIController.Attach(this);

        GameEvents.OnPlayerHealthChanged += OnPlayerHealthChanged;
        if (GameEvents.TryGetComponent2(CameraTarget.MainEntity, out Health health)) 
            OnPlayerHealthChanged(CameraTarget.MainEntity, health);
    }

    private void OnDisable()
    {
        HandUIController.Detach(this);

        GameEvents.OnPlayerHealthChanged -= OnPlayerHealthChanged;
    }

    private void OnPlayerHealthChanged(Entity entity, Health health)
    {
        if (entity != CameraTarget.MainEntity) return;
        
        int overshield = health.Value/Hearts.Length;
        for (int i = 0; i < Hearts.Length; i++)
        {
            Hearts[i].SetValue(overshield + (health.Value%Hearts.Length > i ? 1 : 0));
        }
    }
}