using UnityEngine;

public class ChoiceUIRow : MonoBehaviour, DescriptionUI.ISource
{
    TiledStat m_Stat;
    int m_LevelLow;
    int m_LevelHigh;
    
    public StatIcon Icon;
    public GameObject[] Pips;
    
    public void Setup(TiledStat stat, int low, int boost)
    {
        m_Stat = stat;
        m_LevelLow = low;
        m_LevelHigh = low + boost;
        
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

        data.Title = m_Stat.GetTitle();
        data.Description = m_Stat.GetDescription();

        data.Rows = m_Stat.GetDescriptionRows(m_LevelLow, m_LevelHigh);
        
        return data;
    }
}