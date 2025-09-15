using System;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

public class EnemyTrapMotionTree : EntityLinkMono
{
    private static readonly int Charge = Animator.StringToHash("Charge");

    public Animator animator;
    EnemyTrapMovement.States m_Last;

    private void Update()
    {
        EnemyTrapMovement input = Game.World.EntityManager.GetComponentData<EnemyTrapMovement>(Entity);
        var newState = input.state;
        if (newState != m_Last)
        {
            m_Last = newState;
            if (newState == EnemyTrapMovement.States.Charge)
                animator.SetTrigger(Charge);
        }
    }
}