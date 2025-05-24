using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioSystem : Singleton<AudioSystem>
{
    [SerializeField] private AudioSource musicSource = null;
    [SerializeField] private AudioSource soundSource = null;

    public void PlayMusic(AudioClip clip) {
        if (clip == null)
            return;

        musicSource.clip = clip;
        musicSource.volume = (Player.gameData?.BGMVolume ?? 10f) / 10f;
        musicSource.Play();
    }

    public void StopMusic() {
        musicSource.Stop();
    }

    public void PlaySound(AudioClip clip) {
        if (clip == null)
            return;

        soundSource.clip = clip;
        soundSource.volume = (Player.gameData?.SEVolume ?? 10f) / 10f;
        soundSource.PlayOneShot(clip);
    }

    public void OnConfirmSettings() {
        musicSource.volume = (Player.gameData?.BGMVolume ?? 10f) / 10f;
        soundSource.volume = (Player.gameData?.SEVolume ?? 10f) / 10f;
    }
}
