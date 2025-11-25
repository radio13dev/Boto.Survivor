using Unity.Entities;
using UnityEngine.Events;

public class ShowInInteractRange : EntityLinkMono
{
    public UnityEvent OnInteractStart;
    public UnityEvent OnInteractEnd;
    
    private void OnEnable()
    {
        GameEvents.OnInteractableStart += OnInteractableStart;
        GameEvents.OnInteractableEnd += OnInteractableEnd;
        OnInteractEnd?.Invoke();
    }

    private void OnInteractableStart(Entity entity)
    {
        if (entity != Entity) return;
        OnInteractStart?.Invoke();
    }

    private void OnInteractableEnd(Entity entity)
    {
        if (entity != Entity) return;
        OnInteractEnd?.Invoke();
    }

    private void OnDisable()
    {
        GameEvents.OnInteractableStart -= OnInteractableStart;
        GameEvents.OnInteractableEnd -= OnInteractableEnd;
    }
}