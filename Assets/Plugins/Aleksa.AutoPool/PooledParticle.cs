using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class PooledParticle : AutoPoolBehaviour
{
    public ParticleSystem[] ParticleSystems;
    public override void NewObjectSetup()
    {
        for (int i = 0; i < ParticleSystems.Length; i++)
        {
            ParticleSystems[i].Stop(true);
            ParticleSystems[i].Clear(true);
            ParticleSystems[i].Play(true);
        }
        this.ReturnToPool(ParticleSystems[0].main.duration);
    }

    private void Reset()
    {
        ParticleSystems = GetComponentsInChildren<ParticleSystem>();
    }
}