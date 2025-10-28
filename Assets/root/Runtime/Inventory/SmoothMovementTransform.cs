using UnityEngine;

public class SmoothMovementTransform : MonoBehaviour
{
    public float MovementRate = 10f;
    public float RotationRate = 10f;

    Vector3 m_TargetVector;
    Quaternion m_TargetRotation;

    private void OnEnable()
    {
        transform.position = m_TargetVector;
        transform.rotation = m_TargetRotation;
    }

    private void Update()
    {
        transform.position = TorusMapper.LerpCartesian(transform.position, m_TargetVector, Time.deltaTime * MovementRate);
        //transform.rotation = Quaternion.Slerp(transform.rotation, m_TargetRotation, Time.deltaTime * RotationRate);
    }
    
    public void SetTarget(Vector3 position, Quaternion rotation)
    {
        m_TargetVector = position;
        m_TargetRotation = rotation;
    }
}