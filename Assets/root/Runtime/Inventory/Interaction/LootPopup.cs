using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class LootPopup : MonoBehaviour
{
    public LootPopupOption[] Options;
    Entity m_LastEntity;

    public void Focus(World world, Entity nearestE)
    {
        if (m_LastEntity != nearestE)
        {
            // Update the display
            var lootGen = world.EntityManager.GetComponentData<LootGenerator2>(nearestE);
            for (int i = 0; i < Options.Length; i++)
            {
                Options[i].Setup(lootGen.GetRingStats(i));
            }
        }
        
        if (HandUIController.LastPressed is not LootPopupOption)
        {
            // Open the options and select the first one
            for (int i = 0; i < Options.Length; i++)
                Options[i].gameObject.SetActive(true);
            Options[0].Select();
        }
    }

    private void OnDisable()
    {
        m_LastEntity = Entity.Null;
        for (int i = 0; i < Options.Length; i++)
            Options[i].gameObject.SetActive(false);
    }
    
    public void ToggleFocus()
    {
        if (Options[0].isActiveAndEnabled)
        {
            for (int i = 0; i < Options.Length; i++)
                Options[i].gameObject.SetActive(false);
        }
        else
        {
            for (int i = 0; i < Options.Length; i++)
                Options[i].gameObject.SetActive(true);
            Options[0].Select();
        }
    }
}