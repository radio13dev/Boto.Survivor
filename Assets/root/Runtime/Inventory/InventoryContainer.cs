using UnityEngine;

public class InventoryContainer : MonoBehaviour
{
    public Collider[] Bounds;
    public float ScatterRange = 0.5f;

    public Vector3 GetRandomNearbyPosition(Vector3 transformPosition)
    {
        Collider bestC = null;
        float bestD = float.MaxValue;
        Vector3 bestP = default;
        foreach (var c in Bounds)
        {
            if (!c) continue;
            var p = c.ClosestPoint(transformPosition);
            var d = Vector3.Distance(p, transformPosition);
            if (d < 0.1f) return p; // Early escape if already placed inside
            
            if (d < bestD)
            {
                bestD = d;
                bestC = c;
                bestP = p;
            }
        }
        if (!bestC) return transformPosition;
        
        return bestC.ClosestPoint(bestP + Random.insideUnitSphere*ScatterRange);
    }
    public Vector3 GetRandomPosition()
    {
        if (Bounds.Length == 0) return transform.position;
        var bound = Bounds[Random.Range(0, Bounds.Length)];
        return bound.ClosestPoint(bound.bounds.center + Random.insideUnitSphere * ScatterRange);
    }
}