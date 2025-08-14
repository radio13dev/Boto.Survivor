using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class MotionTreeTest : MonoBehaviour
{
    CameraTarget m_Target;
    public Animator animator;

    private void Start()
    {
        m_Target = GetComponentInParent<CameraTarget>();
    }

    private void Update()
    {
        StepInput input = m_Target.Game.World.EntityManager.GetComponentData<StepInput>(m_Target.Entity);
        animator.SetFloat("movement.x", math.length(input.Direction));
        animator.SetFloat("movement.y", 0);
    }
}
