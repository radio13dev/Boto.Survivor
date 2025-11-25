using System;
using System.Collections.Generic;
using Drawing;
using MIConvexHull;
using Unity.Mathematics;
using UnityEngine;

[ExecuteInEditMode]
public class MapDrawing4 : MonoBehaviourGizmos
{
    public Transform[] Path = Array.Empty<Transform>();
    public float lineWidth;
    public float QuadAlpha = 1f;
    public float ArrowAlpha = 1f;
    public float LeftPathAlpha = 1f;
    public float RightPathAlpha = 1f;
    
    public float threshold = 0.3f;
    public float groundOffset = 1f;
    
    public Color PointColor = Color.white;
    public Color HullColor = Color.green;
    public Color ConvexColor = Color.red;
    
    List<HullVertex2> points = new();
    List<Vector3> hullPoints = new();
    List<Vector3> path = new();
    
    [EditorButton]
    public void Regenerate()
    {
        if (Path.Length < 3)
            return;
        foreach (var path in Path)
            if (!path) return;
            
        List<HullVertex2> pointList = new();
    
        for (int i = 0; i < Path.Length-2; i++)
        {
            float3 a = Path[i].position;
            float3 aN = TorusMapper.GetNormal(a);
            float3 b = Path[i+1].position;
            float3 bN = TorusMapper.GetNormal(b);
            float3 c = Path[i+2].position;
            float3 cN = TorusMapper.GetNormal(c);
            
            var ab = math.normalize(b-a);
            var abN = math.normalize((aN + bN)/2);
            var abR = math.cross(abN, ab);
            
            var bc = math.normalize(c-b);
            var bcN = math.normalize((bN + cN)/2);
            var bcR = math.cross(bcN, bc);
            
            var pL0 = a + abR * 0.5f * lineWidth;
            var pL1 = b + abR * 0.5f * lineWidth;
            var pL2 = b + bcR * 0.5f * lineWidth;
            var pL3 = c + bcR * 0.5f * lineWidth;
            
            var pR0 = a - abR * 0.5f * lineWidth;
            var pR1 = b - abR * 0.5f * lineWidth;
            var pR2 = b - bcR * 0.5f * lineWidth;
            var pR3 = c - bcR * 0.5f * lineWidth;
            
            var dotR = math.dot(ab, bcR);
            var dotR2 = math.dot(ab, bc);
            
            pointList.Add(TorusMapper.CartesianToToroidal(pL0));
            pointList.Add(TorusMapper.CartesianToToroidal((pL0+pL1)/2));
            pointList.Add(TorusMapper.CartesianToToroidal(pL1));
            pointList.Add(TorusMapper.CartesianToToroidal(pR0));
            pointList.Add(TorusMapper.CartesianToToroidal((pR0+pR1)/2));
            pointList.Add(TorusMapper.CartesianToToroidal(pR1));
            if (dotR > 0)
            {
                // Add closest point the 3d lines pr0/pr1 and pr2/pr3 make
                var cn = math.cross(ab, bc);
                var proj = math.dot(pR2 - pR0, ab)*ab;
                var rej = pR2-pR0 - math.dot(pR2-pR0, ab)*ab - math.dot(pR2-pR0, cn)*cn;
                var closest = pR2-bc*math.normalize(rej)/math.dot(bc,math.normalize(rej));
                //pointList.Add(TorusMapper.CartesianToToroidal(closest));
            }
            else
            {
                // same but left side
                var cn = math.cross(ab, bc);
                var proj = math.dot(pL2 - pL0, ab)*ab;
                var rej = pL2-pL0 - math.dot(pL2-pL0, ab)*ab - math.dot(pL2-pL0, cn)*cn;
                var closest = pL2-bc*math.normalize(rej)/math.dot(bc,math.normalize(rej));
                //pointList.Add(TorusMapper.CartesianToToroidal(closest));
            }
            
            if (i == Path.Length-3)
            {
                pointList.Add(TorusMapper.CartesianToToroidal(pL2));
                pointList.Add(TorusMapper.CartesianToToroidal(pR2));
                pointList.Add(TorusMapper.CartesianToToroidal(pL3));
                pointList.Add(TorusMapper.CartesianToToroidal(pR3));
            }
        }
        
        // Create a 2d convex hull around pointList
        points = new(pointList);
        var hull = ConvexHull.Create2D(pointList);
        hullPoints = new(hull.Result.Count);
        foreach (var v in hull.Result)
            hullPoints.Add(TorusMapper.ToroidalToCartesian(v.Position, groundOffset));
            
        List<(float2 Position, bool IsEdge)> pointListWithEdgeFlag = new();
        foreach (var point in pointList)
        {
            foreach (var hullPoint in hull.Result)
            {
                if (math.all(hullPoint.Position == point.Position))
                {
                    pointListWithEdgeFlag.Add((point.Position, true));
                    goto NextPoint;
                }
            }
            pointListWithEdgeFlag.Add((point.Position, false));
            NextPoint:;
        }
        
        // Now, make the concave hull using this algorithm:
        /*
         for i = 1 to the end of the ConcaveList
        {
            find the nearest inner point p k ∈ G from the edge (c i1, c i2);
            // p k should not closer to neighbor edges of (c i1, c i2) than (c i1, c i2)
            
            calculate eh = D(c i1, c i2); // the length of the edge
            calculate dd = DD(p k , {c i1, c i2});

            if (eh/dd) > N // digging process
            {
                insert new edges (c i1, k) and (c i2, k) into the tail of ConcaveList;
                delete edge (c i1, c i2) from the ConcaveList;
            }
        }
        Return ConcaveList;
         */
        var concaveList = hull.Result;
        for (int i = 0; i < concaveList.Count && concaveList.Count < 30; i++)
        {
            var c1 = concaveList[i].Position;
            var c2 = concaveList[(i+1)%concaveList.Count].Position;
            
            var edgeDir = math.normalize(c2 - c1);
            
            int closestInnerIndex = -1;
            float minDist = float.MaxValue;
            float2 closestInner = default;
            for (var pointListIndex = 0; pointListIndex < pointList.Count; pointListIndex++)
            {
                if (pointListWithEdgeFlag[pointListIndex].IsEdge)
                    continue;
                    

                // Check distance to edge
                var p = pointList[pointListIndex].Position;
                var dist = distanceFromEdge(c1,c2,p);
                if (dist > minDist) continue;
                
                // Check distance to neighbor edges
                var prev = concaveList[(i - 1 + concaveList.Count) % concaveList.Count].Position;
                var distPrev = distanceFromEdge(prev,c1,p);
                if (distPrev < dist) continue;
                    
                var next = concaveList[(i + 2) % concaveList.Count].Position;
                var distNext = distanceFromEdge(c2,next,p);
                if (distNext < dist) continue;

                minDist = dist;
                closestInner = p;
                closestInnerIndex = pointListIndex;
            }

            if (closestInnerIndex != -1)
            {
                var eh = math.distance(c1, c2);
                var dd = math.min(math.distance(closestInner, c1), math.distance(closestInner, c2));
                
                if (eh/dd > threshold)
                {
                    // Insert new edges
                    concaveList.Insert(i + 1, closestInner);
                    pointListWithEdgeFlag[closestInnerIndex] = (pointListWithEdgeFlag[closestInnerIndex].Position, true);
                }
            }
        }
        path = new List<Vector3>(concaveList.Count);
        for (int i = 0; i < concaveList.Count; i++)
        {
            path.Add(TorusMapper.ToroidalToCartesian(concaveList[i].Position, groundOffset));
        }
    }
    
    float distanceFromEdge(float2 a, float2 b, float2 p)
    {
        // For a line segment ab and point p, find the distance from p to ab
        // Note: We also handle the fact the line ab has a start and end point
        var ab = b - a;
        var ap = p - a;
        var abLengthSqr = math.dot(ab, ab);
        var t = math.dot(ap, ab) / abLengthSqr;
        t = math.clamp(t, 0, 1);
        var closest = a + t * ab;
        return math.distance(p, closest);
    }
    
    readonly struct HullVertex2 : IVertex2D
    {
        public HullVertex2(float2 p)
        {
            Position = p;
        }
        
        public static implicit operator HullVertex2(float2 p) => new HullVertex2(p);

        public readonly float2 Position;
        public double X => Position.x;
        public double Y => Position.y;
    }

    private void Update()
    {
        Regenerate();
    }

    public override void DrawGizmos()
    {
        using (Draw.WithLineWidth(2, false))
        {
            foreach (var point in points)
            {
                var p = TorusMapper.ToroidalToCartesian(point.Position, groundOffset);
                Draw.SolidCircle(p, TorusMapper.GetNormal(p), 0.2f, PointColor);
            }
            Draw.Polyline(hullPoints, true, HullColor);
            Draw.Polyline(path, true, ConvexColor);
        }
    }
}