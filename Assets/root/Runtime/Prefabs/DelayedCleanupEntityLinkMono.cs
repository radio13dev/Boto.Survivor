using UnityEngine;

public class DelayedCleanupEntityLinkMono : EntityLinkMono
{
    public float DestroyTimeout;
    public Animator DestroyAnimator;
    private static readonly int _Destroy = Animator.StringToHash("Destroy");

    public void StartDestroy()
    {
        Debug.Log($"Started destroy animation for {gameObject.name}");
        DestroyAnimator.SetBool(_Destroy, true);
        Destroy(gameObject, DestroyTimeout);
    }
}