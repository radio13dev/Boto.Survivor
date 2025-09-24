using System;
using TMPro;
using UnityEngine;

public class ChoiceUI : MonoBehaviour
{
    public static ChoiceUI Instance;

    public RingDisplay RingDisplay;
    public TMP_Text Title;
    public TMP_Text Description;
    public ChoiceUIRow[] Rows;

    private void Awake()
    {
        Instance = this;
        Close();
    }

    CompiledStats m_BaseStats;
    public void Setup(CompiledStats stats, Ring ring)
    {
        RingDisplay.UpdateRing(-1, ring);
        var desc = RingDisplay.GetDescription();
        Title.text = desc.Title;
        Description.text = desc.Description;
        
        for (int i = 0; i < Rows.Length; i++)
        {
            if (ring.Stats.GetStatBoost(i, out var stat, out var boost) && boost > 0)
            {
                Rows[i].gameObject.SetActive(true);
                Rows[i].Setup(stat, stats.CompiledStatsTree[stat] - boost, boost);
            }
            else
            {
                Rows[i].gameObject.SetActive(false);
            }
        }
    }

    private void OnDisable()
    {
        if (m_GrabbedDisplay)
        {
            m_GrabbedDisplay.gameObject.SetActive(true);
            m_GrabbedDisplay = null;
        }
    }
    
    RingDisplay m_GrabbedDisplay;
    public void Grab(RingDisplay grabbed)
    {
        if (m_GrabbedDisplay) m_GrabbedDisplay.gameObject.SetActive(true);
        m_GrabbedDisplay = grabbed;
        m_GrabbedDisplay.gameObject.SetActive(false);
        GameEvents.TryGetComponent2<CompiledStats>(CameraTarget.MainTarget.Entity, out var stats);
        Setup(stats, grabbed.Ring);
        RingDisplay.CopyTransform(grabbed);
    }

    private void Close()
    {
        throw new NotImplementedException();
    }
}