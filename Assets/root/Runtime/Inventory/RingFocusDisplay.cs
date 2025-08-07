using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class RingFocusDisplay : MonoBehaviour
{
    public Transform Visual;
    public GemDisplay[] GemDisplays;
    RingDisplay m_Focused;
    public int RingIndex { get; private set; }
    ExclusiveCoroutine m_RotCo;
    ExclusiveCoroutine m_ScaleCo;

    public bool IsFocused(RingDisplay check)
    {
        return m_Focused == check;
    }

    public int GetEquipmentGemIndex(GemDisplay gemSlot)
    {
        return RingIndex*Gem.k_GemsPerRing +  Array.IndexOf(GemDisplays, gemSlot);
    }

    public void UpdateRing(int index, RingDisplay ringDisplay)
    {
        RingIndex = index;
        
        var gems = ringDisplay.Gems;
        for (int i = 0; i < gems.Count && i < GemDisplays.Length; i++)
        {
            GemDisplays[i].UpdateGem(-1, gems[i].Gem);
        }

        if (m_Focused != ringDisplay)
        {
            m_Focused = ringDisplay;
            var rot = 180f + 360f * index / Ring.k_RingCount;
            m_RotCo.StartCoroutine(this, CoroutineHost.Methods.Lerp(Visual, Quaternion.Euler(rot, 90, 90), 0.2f, true, true));
            m_ScaleCo.StartCoroutine(this, ScaleGemsFromZero(0.2f));
        }
    }

    private IEnumerator ScaleGemsFromZero(float duration)
    {
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            t = math.min(t, duration);
                
            float progress = t / duration;
            progress = CoroutineHost.Methods.EaseCubic(progress);
                
            for (int i = 0; i < GemDisplays.Length; i++)
            {
                GemDisplays[i].transform.parent.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, progress);
            }
            yield return null;
        }
    }
}