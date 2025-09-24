using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class RadialLayoutGroup : MonoBehaviour
{
    public float InitAngle;
    public float RadialSpacing;
    public float Radius;
    public bool Expand;

    IEnumerable<Transform> m_Children
    {
        get
        {
            foreach (Transform child in transform)
            {
                if (Expand && !child.gameObject.activeSelf) continue;
                if (child.TryGetComponent<LayoutElement>(out var el) && el.ignoreLayout) continue;
                yield return child;
            }
        }
    }

    [EditorButton]
    public void CalculateSpacing()
    {
        var count = m_Children.Count();
        RadialSpacing = 360f / count;
        ForceRebuild();
    }
    
    [EditorButton]
    public void ForceRebuild()
    {
        int c = m_Children.Count();
        int i = 0;
        foreach (var child in m_Children)
        {
            var angle = InitAngle + i * RadialSpacing;
            if (Expand) angle -= RadialSpacing*(c-1)/2.0f;
            var rad = angle * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * Radius;
            child.localPosition = pos;
            i++;
        }
    }
}
