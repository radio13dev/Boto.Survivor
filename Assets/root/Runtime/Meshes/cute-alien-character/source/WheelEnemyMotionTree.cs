using System;
using Unity.Mathematics;
using Unity.Transforms;
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
    
    public int segments = 15;
    public float Distance = 50;
    public float Width = 3;
    public float Height = 0.1f;

    WheelEnemyMovement.States m_Last;

    private void Awake()
    {
        var mesh = reticleMeshFilter.mesh;
        TorusMapper.CreateRectangularMesh(transform.position, transform.position + transform.forward*Distance, Width, Height, segments, ref mesh);
        reticleMeshFilter.transform.SetParent(transform.parent, false); // Detach it
        reticleMeshFilter.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        reticleMeshFilter.transform.localScale = Vector3.one;
        reticleMeshFilter.gameObject.SetActive(true);
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
                var t = Game.World.EntityManager.GetComponentData<LocalTransform>(Entity);
                var a =  t.Position;
                var b =  t.Position + t.Forward() * Distance;
                Debug.Log($"Regenerating mesh...");

                var adjustedVerts = reticleMeshFilter.mesh.vertices;
                if (segments != adjustedVerts.Length/3 - 1)
                {
                    var mesh = reticleMeshFilter.mesh;
                    TorusMapper.CreateRectangularMesh(a, b, Width, Height, segments, ref mesh);
                }
                else
                {
                    TorusMapper.CreateRectangularMesh(a, b, Width, Height, ref adjustedVerts);
                    reticleMeshFilter.mesh.vertices = adjustedVerts;
                }
                reticleMeshFilter.transform.position = a;
                reticleMeshFilter.gameObject.SetActive(true);
            }
            else if (newState == WheelEnemyMovement.States.ChargeAttack)
                animator.SetTrigger(ChargeAttack);
            else if (newState == WheelEnemyMovement.States.ChargeEnd)
            {
                reticleMeshFilter.gameObject.SetActive(false);
                animator.SetTrigger(ChargeEnd);
            }
        }
    }

    private void OnDisable()
    {
        if (reticleMeshFilter)
            reticleMeshFilter.transform.SetParent(transform);
    }

    private void OnDestroy()
    {
        if (reticleMeshFilter)
            Destroy(reticleMeshFilter.gameObject);
    }
}