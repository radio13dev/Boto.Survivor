using Unity.Entities;
using UnityEngine;
using UnityEngine.Events;

public class ShowInInteractRange : EntityLinkMono
{
    public UnityEvent OnInteractStart;
    public UnityEvent OnInteractEnd;
    
    private void OnEnable()
    {
        GameEvents.OnEvent += OnGameEvent;
        OnInteractEnd?.Invoke();
    }

    private void OnDisable()
    {
        GameEvents.OnEvent -= OnGameEvent;
    }

    private void OnGameEvent(GameEvents.Type eType, Entity entity)
    {
        if (eType != GameEvents.Type.InteractableStart && eType != GameEvents.Type.InteractableEnd) return;
        if (entity != Entity) return;
        
        if (eType == GameEvents.Type.InteractableStart)
        {
            OnInteractStart?.Invoke();
        }
        else
        {
            OnInteractEnd?.Invoke();
        }
    }
}
