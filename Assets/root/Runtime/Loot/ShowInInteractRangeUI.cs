using Unity.Entities;
using UnityEngine;
using UnityEngine.Events;

public class ShowInInteractRangeUI : MonoBehaviour
{
    public UnityEvent OnInteractStart;
    public UnityEvent OnInteractEnd;
    public Entity m_Entity;
    
    private void OnEnable()
    {
        GameEvents.OnInteractableStart += OnInteractableStart;
        GameEvents.OnInteractableEnd += OnInteractableEnd;
        ForceCheckState();
    }

    private void OnDisable()
    {
        GameEvents.OnInteractableStart -= OnInteractableStart;
        GameEvents.OnInteractableEnd -= OnInteractableEnd;
    }

    private void OnInteractableStart(Entity entity)
    {
        if (entity == Entity.Null) return;
        m_Entity = entity;
        OnInteractStart?.Invoke();
    }

    private void OnInteractableEnd(Entity entity)
    {
        if (entity == Entity.Null) return;
        m_Entity = Entity.Null;
        OnInteractEnd?.Invoke();
    }

    private void ForceCheckState()
    {
        if (Game.ClientGame == null) return;
        if (!GameEvents.TryGetSingleton<NearestInteractable>(out var nearest) || nearest.Value == Entity.Null)
        {
            m_Entity = Entity.Null;
            OnInteractEnd?.Invoke();
        }
        else
        {
            m_Entity = nearest.Value;
            OnInteractStart?.Invoke();
        }
    }
}