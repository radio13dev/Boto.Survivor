using System.Collections;
using UnityEngine;

public abstract class SoundController : MonoBehaviour
{
    public IEnumerator FadeIn(AudioSource source, float f)
    {
        float t = 0;
        while (t <= f)
        {
            source.volume = Mathf.Lerp(0, 1, t / f);
            yield return null;
            t += Time.deltaTime;
        }
        source.volume = 1;
    }
}