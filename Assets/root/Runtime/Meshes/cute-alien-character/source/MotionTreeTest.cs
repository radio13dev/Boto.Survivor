using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class MotionTreeTest : MonoBehaviour
{
    public Animator animator;

    private void Update()
    {
        float2 movement = new float2();
        if (Keyboard.current.wKey.isPressed) movement.y += 1f;
        if (Keyboard.current.sKey.isPressed) movement.y -= 1f;
        if (Keyboard.current.aKey.isPressed) movement.x -= 1f;
        if (Keyboard.current.dKey.isPressed) movement.x += 1f;
        
        animator.SetFloat("movement.x", movement.x);
        animator.SetFloat("movement.y", movement.y);
    }
}
