using Unity.Mathematics;
using UnityEngine;

public class SyncedTransformOverTime : MonoBehaviour
{
    public ease.Mode easeMode = ease.Mode.cubic_out;
    public float rate = 1;
    public AnimationCurve xScaleCurve = AnimationCurve.Constant(0,1,1);
    public AnimationCurve yScaleCurve = AnimationCurve.Constant(0,1,1);

    void Update()
    {
        
        var t = easeMode.Evaluate((math.sin(Time.time*rate)+1)/2);
        transform.localScale = new Vector3(xScaleCurve.Evaluate(t), yScaleCurve.Evaluate(t), 1);
    }
}
