using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class MotionTreeTest : MonoBehaviour
{
    CameraTarget m_Target;
    float m_Last;
    public Animator animator;

    private void Start()
    {
        m_Target = GetComponentInParent<CameraTarget>();
    }

    private void Update()
    {
        StepInput input = m_Target.Game.World.EntityManager.GetComponentData<StepInput>(m_Target.Entity);
        m_Last = Mathf.MoveTowards(m_Last, math.length(input.Direction), Time.deltaTime*10f);
        animator.SetFloat("movement.x", m_Last);
        animator.SetFloat("movement.y", 0);
    }
}
