using Unity.Mathematics;
using UnityEngine;

public class Heart : MonoBehaviour
{
    public MeshRenderer Renderer;
    public Material[] ValueMats;
    public Material InvincibleMat;
    public PooledParticle IncreaseParticle;
    public PooledParticle DecreaseParticle;
    int _last = 0;

    public void SetValue(int value)
    {
        if (_last != value)
        {
            PooledParticle created;
            if (_last < value) created = IncreaseParticle.GetFromPool();
            else created = DecreaseParticle.GetFromPool();
            created.transform.SetPositionAndRotation(transform.position, transform.rotation);
            created.transform.localScale = Vector3.one*50;
            
            _last = value;
        }
        Renderer.material = ValueMats[math.clamp(value, 0, ValueMats.Length - 1)];
    }
}