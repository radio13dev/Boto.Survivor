using System;
using UnityEngine;

public class RingFocusDisplay : MonoBehaviour
{
    public GemDisplay[] GemDisplays;
    int m_FocusedRingIndex = -1;

    private void Awake()
    {
        for (int i = 0; i < GemDisplays.Length; i++)
            GemDisplays[i].gameObject.SetActive(false);
    }

    public bool IsFocused(int i)
    {
        return m_FocusedRingIndex == i;
    }

    public void UpdateRing(RingDisplay ringDisplay)
    {
        var gems = ringDisplay.Gems;
        for (int i = 0; i < gems.Count && i < GemDisplays.Length; i++)
        {
            GemDisplays[i].gameObject.SetActive(gems[i].Equipped);
            if (gems[i].Equipped)
                GemDisplays[i].UpdateGem(gems[i].Gem);
        }
    }
}