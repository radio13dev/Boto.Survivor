using System;
using System.Text;
using TMPro;
using Unity.Entities;
using UnityEngine;

public class InstanceCounts : MonoBehaviour
{
    public TMP_Text Display;
    bool initComplete = false;
    EntityQuery query;
    int instanceTypes;

    private void Update()
    {
        if (!initComplete)
        {
            if (Game.ClientGame == null || !Game.ClientGame.IsReady) return;
            
            initComplete = true;
            query = Game.ClientGame.World.EntityManager.CreateEntityQuery(new ComponentType(typeof(InstancedResourceRequest), ComponentType.AccessMode.ReadOnly));
            
            instanceTypes = Game.ClientGame.World.EntityManager.CreateEntityQuery(new ComponentType(typeof(GameManager.InstancedResources), ComponentType.AccessMode.ReadOnly))
                .GetSingletonBuffer<GameManager.InstancedResources>()
                .Length;
        }
        
        StringBuilder sb = new();
        for (int i = 0; i < instanceTypes; i++)
        {
            query.SetSharedComponentFilter(new InstancedResourceRequest(i));
            sb.AppendLine($"{i}: {query.CalculateEntityCount()}");
        }
        Display.text = sb.ToString();
    }
}
