using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class MotionTreeTest : EntityLinkMono
{
    private static readonly int Speed = Animator.StringToHash("Speed");
    float m_Last;
    public Animator animator;

    private void Update()
    {
        StepInput input = Game.World.EntityManager.GetComponentData<StepInput>(Entity);
        m_Last = Mathf.MoveTowards(m_Last, math.length(input.Direction), Time.deltaTime*10f);
        animator.SetFloat(Speed, m_Last);
    }
}
