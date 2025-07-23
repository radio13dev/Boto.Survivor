using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class PlaySoundOnEnable : MonoBehaviour
{
    public new PooledAudio audio;
    public AudioClip clip;
    public AudioMixerGroup mixer;

    private void OnEnable()
    {
        if (audio.GetPoolActiveCount() > PooledAudio.k_MaxAudioSources) return;
        StartCoroutine(DelayedStart());
    }
    
    IEnumerator DelayedStart()
    {
        yield return null;
        var player = audio.GetFromPool();
        player.transform.SetPositionAndRotation(transform.position, transform.rotation);
        player.Setup(clip, mixer);
    }
}