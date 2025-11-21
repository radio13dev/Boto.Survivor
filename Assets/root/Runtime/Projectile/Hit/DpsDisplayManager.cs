using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class DpsDisplayManager : MonoBehaviour
{
    public DpsDisplay DpsDisplayTemplate;
    public Transform DpsDisplayContainer;
    
    Dictionary<Entity, DpsDisplay> DpsDisplayLookup = new Dictionary<Entity, DpsDisplay>();   

    private void OnEnable()
    {
        GameEvents.OnEnemyHealthChanged += OnEnemyHealthChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnEnemyHealthChanged -= OnEnemyHealthChanged;
        
        foreach (var val in DpsDisplayLookup)
            val.Value.ReturnToPool();
        DpsDisplayLookup.Clear();
    }

    private void OnEnemyHealthChanged(Entity entity, Health newhealth, int changereceived)
    {
        if (!GameEvents.TryGetComponent2<Health>(entity, out var health))
        {
            health = new Health();
        }
        
        if (!DpsDisplayLookup.TryGetValue(entity, out var display))
        {
            DpsDisplayLookup[entity] = display = DpsDisplayTemplate.GetFromPool();
            display.transform.SetParent(DpsDisplayContainer);
            display.Setup(this, entity, health.InitHealth, health.Value);
        }
        
        if (GameEvents.TryGetComponent2<LocalTransform>(entity, out var localTransform))
        {
            display.transform.SetPositionAndRotation(localTransform.Position, localTransform.Rotation);
        }
        
        display.AddHealth(changereceived);
    }

    public void Remove(Entity entity)
    {
        if (DpsDisplayLookup.Remove(entity, out var display))
            display.ReturnToPool();
    }
}