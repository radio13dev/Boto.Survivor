using Unity.Mathematics;
using UnityEngine;

[ExecuteInEditMode]
public class UIFocusMini : MonoBehaviour
{
    public Transform Target;
    public float FadeDelay = 0.1f;
    public float FadeDuration = 0.2f;
    public float Acceleration = 1;
    public float MoveSpeed = 1;
    public float MoveSpeedMax = 1;
    public float MoveSpeedLinear = 1f;
    public float ScaleSpeed = 1;
    
    // Allows overshooting
    Vector3 m_Velocity;
    Quaternion m_VelocityRot;
    
    // So we keep moving when target deselected
    Vector3 m_LastTargetPos;
    Quaternion m_LastTargetRot;
    
    // To delay the fade out
    float m_TimeSinceDeselect;

    private void Update()
    {
        var dt = math.clamp(Time.deltaTime, 0, 0.1f);
        if (Target)
        {
            m_LastTargetPos = Target.position;
            m_LastTargetRot = Target.rotation;
            
            transform.localScale = math.lerp(transform.localScale, Vector3.one, math.clamp(dt * ScaleSpeed, 0, 1));
            
            // On first select: Instantly snap to position.
            if (m_TimeSinceDeselect > 0)
            {
                m_TimeSinceDeselect = 0;
                transform.position = m_LastTargetPos;
            }
        }
        else
        {
            m_TimeSinceDeselect += dt;
            if (m_TimeSinceDeselect < FadeDelay)
            {
                // Nothing
            }
            else
            {
                var scale = math.lerp(1, 0, math.clamp((m_TimeSinceDeselect-FadeDelay)/FadeDuration, 0, 1));
                transform.localScale = Vector3.one*scale;
            }
        }
        
        // Change velocity to get us to move towards the target
        var targetVel = (m_LastTargetPos - transform.position)*MoveSpeed;
        if (targetVel.magnitude > MoveSpeedMax)
            targetVel = targetVel.normalized*MoveSpeedMax;
        
        m_Velocity = math.lerp(m_Velocity, targetVel, dt * Acceleration);
        
        //var targetVelRot = Quaternion.(transform.rotation, m_LastTargetRot, RotateSpeed)*Quaternion.Inverse(transform.rotation);
        //m_VelocityRot = math.slerp(m_VelocityRot, targetVelRot, dt * AccelerationRot);
        
        // Move to target
        transform.position += m_Velocity*dt;
        transform.position = mathu.MoveTowards(transform.position, m_LastTargetPos, dt * MoveSpeedLinear);
        //transform.rotation = math.slerp(transform.rotation, m_VelocityRot, dt*RotateSpeed);
    }
}