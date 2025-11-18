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
        GameEvents.OnEvent += OnGameEvent;
        ForceCheckState();
    }

    private void OnDisable()
    {
        GameEvents.OnEvent -= OnGameEvent;
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

    public virtual void OnGameEvent(GameEvents.Data data)
    {
        var eType = data.Type;
        if (eType != GameEvents.Type.InteractableStart && eType != GameEvents.Type.InteractableEnd) return;
        if (data.Entity == Entity.Null) return;
        
        if (eType == GameEvents.Type.InteractableStart)
        {
            m_Entity = data.Entity;
            OnInteractStart?.Invoke();
        }
        else
        {
            m_Entity = Entity.Null;
            OnInteractEnd?.Invoke();
        }
    }
}