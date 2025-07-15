using System;
using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

public class FPS : MonoBehaviour
{
    public TMP_Text Display;
    public Graph FpsGraph;
    public Graph FpsGraphCondensed;
    List<float2> m_FpsHistory = new();
    int m_FrameCount;

    private void Update()
    {
        Display.text = (1.0f/Time.deltaTime).ToString("N2");
        
        if (FpsGraph)
        {
            FpsGraph.Show(new Graph.DataSet(m_FpsHistory), 0, m_FpsHistory.Count, 0, 0.1f, true);
        }
        if (FpsGraphCondensed)
        {
            List<float2> condensed = new();
            for (int i = 0; i < m_FpsHistory.Count; i++)
            {
                int index = i/10;
            }
        }
    }
}
