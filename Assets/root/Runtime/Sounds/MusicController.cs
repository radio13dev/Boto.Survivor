using System.Collections;
using UnityEngine;

public class MusicController : SoundController
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
        source.loop = true;
        source.volume = 0;
        source.Play();
        StartCoroutine(FadeIn(source, 1f));
    }
}