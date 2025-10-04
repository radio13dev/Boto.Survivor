using System;
using Drawing;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Rotates smoothly over time but the up direction always remains within a cone defined by the bounds.
/// </summary>
public class RotateOverTimeWithBounds : MonoBehaviourGizmos
{
    public float ConeAngle;
    public float Speed;
    public float Accuracy = 0.01f;
    
    float3 targetUp;
    float3 lerpUp;
    float3 targetForward;
    float3 lerpForward;

    [EditorButton]
    private void Awake()
    {
        targetUp = lerpUp = math.up();
        targetForward = lerpForward = math.forward();
        transform.localRotation = Quaternion.LookRotation(targetForward, targetUp);
        
        Update();
        
        targetUp = lerpUp;
        targetForward = lerpForward;
        transform.localRotation = Quaternion.LookRotation(targetForward, targetUp);
    }

    private void Update()
    {
        // Rotate the up and forward directions smoothly
        var newUp = math.lerp(math.mul(transform.localRotation, math.up()), targetUp, Speed * Time.deltaTime);
        if (math.all(targetUp != lerpUp))
        {
            targetUp = mathu.MoveTowards(targetUp, lerpUp, Speed * Time.deltaTime);
        }
        else
        {
            // Chose a random 'up' direction within ConeAngle of the world up direction
            var angle = UnityEngine.Random.Range(0, ConeAngle);
            var axis = math.normalize(math.cross(math.up(), Random.onUnitSphere));
            var rot = quaternion.AxisAngle(axis, angle);
            lerpUp = math.mul(rot, math.up());
        }
        
        var newForward = math.lerp(math.mul(transform.localRotation, math.forward()), targetForward, Speed * Time.deltaTime);
        if (math.all(targetForward != lerpForward))
        {
            targetForward = mathu.MoveTowards(targetForward, lerpForward, Speed * Time.deltaTime);
        }
        else
        {
            // Chose a random 'forward' direction perpendicular to the up direction
            var axis = targetUp;
            var angle = UnityEngine.Random.Range(0, math.PI * 2);
            var rot = quaternion.AxisAngle(axis, angle);
            lerpForward = math.mul(rot, math.right());
        }
        
        transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(targetForward, targetUp), Speed*Time.deltaTime);
    }

    public override void DrawGizmos()
    {
        var draw = Draw.editor;
        
        float3 Center = transform.position;
        float3 ConeDir = transform.parent.up;
        float TorusMin = 0.1f;
        float Radius = 1f;
        var color = new Color(0.3f, 0.5f, 1f, 0.5f);

        // Draw an arrow pointing in the direction of the cone, from the TorusMin to the Radius
        draw.Arrow((float3)Center + ConeDir * TorusMin, (float3)Center + ConeDir * Radius, math.up(), 0.1f, color * new Color(0, 1f, 0.3f, 0.5f));

        // Draw a circle at the end of the arrow, with a radius based on the cone angle
        // The cone is a 'slice' of a sphere, so the circle radius rests on the spheres surface.
        // The min radius is 0, the max radius is 'Radius'. The circle should also be 'inset' from the edge of the sphere based on the angle.
        var outerCircleShift = (Radius * Mathf.Cos(ConeAngle));
        var outerCircleCenter = ConeDir * outerCircleShift;
        var outerCircleRadius = Radius * Mathf.Sin(ConeAngle);
        draw.Circle((float3)Center + outerCircleCenter, ConeDir * math.sign(outerCircleShift), outerCircleRadius, color * new Color(0, 1f, 0.3f, 0.5f));

        // Also draw a circle at the start of the arrow
        var innerCircleShift = (TorusMin * Mathf.Cos(ConeAngle));
        var innerCircleCenter = ConeDir * innerCircleShift;
        var innerCircleRadius = TorusMin * Mathf.Sin(ConeAngle);
        draw.Circle((float3)Center + innerCircleCenter, ConeDir * math.sign(innerCircleShift), innerCircleRadius, color * new Color(1f, 0.3f, 0.3f, 0.5f));

        // Now draw arrows connecting the two circles
        var steps = 8;
        for (int i = 0; i < steps; i++)
        {
            var angle = (i / (float)steps) * math.PI * 2;
            var circleDir = new float3(math.cos(angle), math.sin(angle), 0);
            var rot = quaternion.LookRotationSafe(circleDir, ConeDir);
            var innerPoint = (float3)Center + innerCircleCenter + math.mul(rot, new float3(innerCircleRadius, 0, 0));
            var outerPoint = (float3)Center + outerCircleCenter + math.mul(rot, new float3(outerCircleRadius, 0, 0));
            draw.Line(innerPoint, outerPoint, color * new Color(0, 1f, 0.3f, 0.5f));
        }
    }
}