using System.Collections;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class DamageNumber : AutoPoolBehaviour
{
    const float RandomJumpDirectionStrength = 0.1f;
    const float JumpStrength = 7f;
    const float Gravity = 10;

    public TMP_Text Text;

    public override void NewObjectSetup()
    {
    }
    
    public void Setup(Transform zeroPos, int change, float duration)
    {
        transform.SetPositionAndRotation(zeroPos.position, zeroPos.rotation);
        
        Text.text = math.abs(change).ToString("N0");
        Text.color = change == 0 ? Palette.HealthChangeZero : change > 0 ? Palette.HealthChangePositive : Palette.HealthChangeNegative;
        this.ReturnToPool(duration);
        StartCoroutine(FadeOut(duration));
        StartCoroutine(Bounce((transform.up + Random.insideUnitSphere * RandomJumpDirectionStrength)*JumpStrength));
    }
    
    IEnumerator FadeOut(float duration)
    {
        var initDuration = duration;
        var initColor = Text.color;
        var zeroColor = initColor; zeroColor.a = 0;
        
        while (duration > 0)
        {
            duration -= Time.deltaTime;
            var t = math.clamp(1 - (duration/initDuration), 0, 1);
            
            Text.color = Color.Lerp(initColor, zeroColor, ease.cubic_in(t));
            yield return null;
        }
    }
    
    IEnumerator Bounce(Vector3 velocity)
    {
        while (true)
        {
            velocity += -transform.up*Time.deltaTime*Gravity;
            transform.localPosition += velocity*Time.deltaTime;
            yield return null;
        }
    }
}