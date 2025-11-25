using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class ShiftOverTime : MonoBehaviour
{
    public float Speed = 1f;
    public float Amount = 1f;
    public float3 Direction = new float3(1,0,0);
    public float Accuracy = 0.01f;
    
    public float m_Current;
    public float m_CurrentTarget;

    [EditorButton]
    private void Awake()
    {
        Update();
        m_Current = m_CurrentTarget;
        Update();
    }

    private void Update()
    {
        m_Current = math.lerp(m_Current, m_CurrentTarget, Speed * Time.deltaTime);
        transform.localPosition = Direction * m_Current;
        
        if (math.abs(m_Current - m_CurrentTarget) < Accuracy)
        {
            m_CurrentTarget = Random.value*Amount;
            if (m_Current >= 0) m_CurrentTarget = -m_CurrentTarget;
        }
    }
}