using UnityEngine;
using Random = UnityEngine.Random;

public class AmbientController : SoundController
{
    private void OnEnable()
    {
        var source = GetComponent<AudioSource>();
        if (!source || !source.clip)
        {
            Debug.LogWarning($"No source or clip attached to ambient controller.");
            enabled = false;
            return;
        }
    
        source.Stop();
        source.time = Random.Range(0, source.clip.length);
        source.loop = true;
        source.volume = 0;
        source.Play();
        StartCoroutine(FadeIn(source, 5f));
    }
}