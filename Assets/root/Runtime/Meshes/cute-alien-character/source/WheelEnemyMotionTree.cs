using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class WheelEnemyMotionTree : EntityLinkMono
{
    private static readonly int ChargeStart = Animator.StringToHash("ChargeStart");
    private static readonly int ChargeAttack = Animator.StringToHash("ChargeAttack");
    private static readonly int ChargeEnd = Animator.StringToHash("ChargeEnd");
    
    byte m_Last;
    public Animator animator;

    private void Update()
    {
        WheelEnemyMovement input = Game.World.EntityManager.GetComponentData<WheelEnemyMovement>(Entity);
        var newState = input.state;
        if (newState != m_Last)
        {
            m_Last = newState;
            if (newState == 1)
                animator.SetTrigger(ChargeStart);
            else if (newState == 2)
                animator.SetTrigger(ChargeAttack);
            else
                animator.SetTrigger(ChargeEnd);
        }
    }
}
