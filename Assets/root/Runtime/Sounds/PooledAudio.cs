using UnityEngine;
using UnityEngine.Audio;

public class PooledAudio : AutoPoolBehaviour
{
    public const float k_MaxAudioSources = 50;

    public AudioSource source;
    public override void NewObjectSetup() { }

    public void Setup(AudioClip clip, AudioMixerGroup mixer)
    {
        source.outputAudioMixerGroup = mixer;
        source.PlayOneShot(clip);
        this.ReturnToPool(clip.length);
    }
}