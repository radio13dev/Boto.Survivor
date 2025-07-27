using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

public class PlayRandomSoundOnEnable : MonoBehaviour
{
    public new PooledAudio audio;
    public AudioClip[] clips;
    public AudioMixerGroup mixer;

    private void OnEnable()
    {
        if (audio.GetPoolActiveCount() >  PooledAudio.k_MaxAudioSources) return;
        StartCoroutine(DelayedStart());
    }
    
    IEnumerator DelayedStart()
    {
        yield return null;
        var player = audio.GetFromPool();
        player.transform.SetPositionAndRotation(transform.position, transform.rotation);
        player.Setup(clips[Random.Range(0, clips.Length)], mixer);
    }
}