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
        GameEvents.OnEvent += OnGameEvent;
    }

    private void OnDisable()
    {
        GameEvents.OnEvent -= OnGameEvent;
        
        foreach (var val in DpsDisplayLookup)
            val.Value.ReturnToPool();
        DpsDisplayLookup.Clear();
    }

    private void OnGameEvent(GameEvents.Data data)
    {
        var eType = data.Type; var entity = data.Entity;
        if (eType != GameEvents.Type.EnemyHealthChanged) return;
        if (!GameEvents.TryGetComponent2<Health>(entity, out var health))
        {
            health = new Health();
        }
        
        if (!DpsDisplayLookup.TryGetValue(entity, out var display))
        {
            DpsDisplayLookup[entity] = display = DpsDisplayTemplate.GetFromPool();
            display.transform.SetParent(DpsDisplayContainer);
            display.Setup(this, data.Entity, health.InitHealth, health.Value);
        }
        
        if (GameEvents.TryGetComponent2<LocalTransform>(entity, out var localTransform))
        {
            display.transform.SetPositionAndRotation(localTransform.Position, localTransform.Rotation);
        }
        
        display.AddHealth(data.Int0);
    }

    public void Remove(Entity entity)
    {
        if (DpsDisplayLookup.Remove(entity, out var display))
            display.ReturnToPool();
    }
}