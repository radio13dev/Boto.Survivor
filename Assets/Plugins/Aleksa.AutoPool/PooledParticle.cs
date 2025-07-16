using UnityEngine;

public class PooledParticle : AutoPoolBehaviour
{
    public ParticleSystem particleSystem;
    public override void NewObjectSetup()
    {
        particleSystem.Stop(true);
        particleSystem.Clear(true);
        particleSystem.Play(true);
        this.ReturnToPool(particleSystem.main.duration);
    }

    private void Reset()
    {
        particleSystem = GetComponent<ParticleSystem>();
    }
}