using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CirclableVisual : EntityLinkMono
{
    private static readonly int Fill = Shader.PropertyToID("_Fill");
    public DecalProjector projector;

    private void Awake()
    {
        // Turn the initial material into an instanced one
        projector.material = new Material(projector.material);
    }

    private void Update()
    {
        if (!HasLink()) return;
        if (!GameEvents.TryGetComponent2<Circlable>(Entity, out Circlable c)) return;
        projector.material.SetFloat(Fill, c.Charge/c.MaxCharge);
    }

    private void OnDestroy()
    {
        if (projector.material)
            Destroy(projector.material);
    }
}
