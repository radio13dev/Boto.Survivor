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
        OnInteractEnd?.Invoke();
    }

    private void OnDisable()
    {
        GameEvents.OnEvent -= OnGameEvent;
    }

    private void OnGameEvent(GameEvents.Data data)
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
            OnInteractEnd?.Invoke();
        }
    }
}