using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class WheelEnemyMotionTree : EntityLinkMono
{
    private static readonly int ChargeStart = Animator.StringToHash("ChargeStart");
    private static readonly int ChargeAttack = Animator.StringToHash("ChargeAttack");
    private static readonly int ChargeEnd = Animator.StringToHash("ChargeEnd");

    public Animator animator;
    public MeshRenderer reticleMeshRenderer;
    public MeshFilter reticleMeshFilter;
    
    public float Distance = 50;
    public float Width = 3;

    WheelEnemyMovement.States m_Last;

    private void Awake()
    {
        TorusMapper.SnapToSurface(math.float3(1, 0, 0), 0, out var a, out _);
        TorusMapper.SnapToSurface(math.float3(1, 0, 0.1f), 0, out var b, out _);
        reticleMeshFilter.mesh = TorusMapper.CreateRectangularMesh(a, b, 0.5f, 8);
        reticleMeshFilter.transform.SetParent(transform.parent, false); // Detach it
        reticleMeshFilter.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        reticleMeshFilter.transform.localScale = Vector3.one;
    }
    
    [EditorButton]
    public void DebugMesh()
    {
        TorusMapper.SnapToSurface(math.float3(20, 0, 0), 0, out var a, out _);
        TorusMapper.SnapToSurface(math.float3(0, 10, 100f), 0, out var b, out _);
        reticleMeshFilter.mesh = TorusMapper.CreateRectangularMesh(a, b, 10f, 20);
    }

    private void Update()
    {
        WheelEnemyMovement input = Game.World.EntityManager.GetComponentData<WheelEnemyMovement>(Entity);
        var newState = input.state;
        if (newState != m_Last)
        {
            m_Last = newState;
            if (newState == WheelEnemyMovement.States.ChargeStart)
                animator.SetTrigger(ChargeStart);
            else if (newState == WheelEnemyMovement.States.ChargeAimingComplete)
            {
                // Create the attack reticle here
                var a = transform.position;
                var b = transform.position + transform.forward * Distance;
                Debug.Log($"Regenerating mesh...");

                var adjustedVerts = reticleMeshFilter.mesh.vertices;
                TorusMapper.CreateRectangularMesh(a, Distance, Width, ref adjustedVerts);
                reticleMeshFilter.mesh.vertices = adjustedVerts;
            }
            else if (newState == WheelEnemyMovement.States.ChargeAttack)
                animator.SetTrigger(ChargeAttack);
            else if (newState == WheelEnemyMovement.States.ChargeEnd)
                animator.SetTrigger(ChargeEnd);
        }
    }
}