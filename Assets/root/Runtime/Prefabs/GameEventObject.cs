using Drawing;
using UnityEngine;

public class GameEventObject : MonoBehaviourGizmos
{
    public float ExclusionRadius;

    public override void DrawGizmos()
    {
        using (Draw.WithLineWidth(2, false))
        {
            Draw.Arrowhead(transform.position, TorusMapper.GetNormal(transform.position), 4, new Color(0, 0, 0, 0.4f));
            Draw.Circle(transform.position, TorusMapper.GetNormal(transform.position), ExclusionRadius, new Color(0, 0, 0, 0.4f));
        }
    }
}