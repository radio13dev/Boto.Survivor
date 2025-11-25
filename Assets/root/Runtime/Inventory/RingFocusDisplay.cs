using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class RingFocusDisplay : MonoBehaviour
{
    public Transform Visual;
    public GemDisplay[] GemDisplays;
    RingDisplay m_Focused;
    public int RingIndex;
    ExclusiveCoroutine m_RotCo;
    ExclusiveCoroutine m_ScaleCo;

    public bool IsFocused(RingDisplay check)
    {
        return m_Focused == check;
    }

    public void UpdateRing(int index, RingDisplay ringDisplay)
    {
        RingIndex = index;
        
        var gems = ringDisplay.Gems;
        for (int i = 0; i < gems.Count && i < GemDisplays.Length; i++)
        {
            GemDisplays[i].UpdateGem(index*Gem.k_GemsPerRing + i, gems[i].Gem);
            if (UIFocus.Interact != GemDisplays[i].gameObject)
                GemDisplays[i].SnapBackToOrigin();
        }

        if (m_Focused != ringDisplay)
        {
            m_Focused = ringDisplay;
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