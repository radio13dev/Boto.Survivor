using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class RandomPositioner : MonoBehaviour
{
    public float radius;

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(default, radius);
    }

    [EditorButton]
    public void RandomisePositionAroundTorus()
    {
        transform.position = Random.onUnitSphere * radius;
        transform.rotation = Random.rotationUniform;
    }
}
