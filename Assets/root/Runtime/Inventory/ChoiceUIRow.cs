using UnityEngine;

public class ChoiceUIRow : MonoBehaviour, DescriptionUI.ISource
{
    TiledStat m_Stat;
    int m_LevelLow;
    int m_LevelHigh;
    RingDisplay m_RingDisplayParent;
    
    public StatIcon Icon;
    public GameObject[] Pips;
    
    public void Setup(TiledStat stat, int low, int boost, RingDisplay ringDisplayParent)
    {
        m_Stat = stat;
        m_LevelLow = low;
        m_LevelHigh = low + boost;
        m_RingDisplayParent = ringDisplayParent;
        
        Icon.SetStat(stat);
        for (int j = 0; j < Pips.Length; j++)
        {
            Pips[j].SetActive(j < boost);
        }
        GetComponentInChildren<RadialLayoutGroup>().ForceRebuild();
    }
    
    public DescriptionUI.Data GetDescription()
    {
        var data = new DescriptionUI.Data();

        if (m_RingDisplayParent)
            data = m_RingDisplayParent.GetDescription();
        
        data.Title = m_Stat.GetTitle();
        data.Description = m_Stat.GetDescription();

        data.Rows = m_Stat.GetDescriptionRows(m_LevelLow, m_LevelHigh);
        
        
        return data;
    }
    
    FocusableRequest m_Focusable;
    public void OnFocusRequestStart()
    {
        OnFocusRequestEnd();
        var tiles = Object.FindAnyObjectByType<TiledStatsUI>().Tiles;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i].Stat != this.m_Stat) continue;
            if (!tiles[i].TryGetComponent<FocusableRequest>(out var focusable)) continue;
            if (!focusable.enabled) continue;
            m_Focusable = focusable;
            UIFocusRequest.StartFocus(m_Focusable);
        }
    }
    public void OnFocusRequestEnd()
    {
        if (m_Focusable)
            UIFocusRequest.EndFocus(m_Focusable);
        m_Focusable = null;
    }
}