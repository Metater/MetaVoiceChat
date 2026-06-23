using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

// https://johnleonardfrench.com/how-to-fade-audio-in-unity-i-tested-every-method-this-ones-the-best/#second_method

namespace Metater
{
    public static class MetaAudio
    {
        public static void PlayOneShot(this AudioSource audioSource, float volumeScale = 1.0f)
        {
            audioSource.PlayOneShot(audioSource.clip, volumeScale);
        }

        /// <summary>
        /// This assumes the AudioSource and AudioListener have a low relative velocity
        /// </summary>
        public static float PlayRealistically(this AudioSource audioSource, float speedOfSound = 343)
        {
            float delaySeconds = GetDelaySeconds(audioSource.transform.position, speedOfSound);
            audioSource.PlayDelayed(delaySeconds);
            return delaySeconds;
        }

        /// <summary>
        /// This assumes the AudioSource and AudioListener have a low relative velocity
        /// </summary>
        public static float GetDelaySeconds(Vector3 sourcePosition, float speedOfSound = 343)
        {
            var listener = MetaCache.Object<AudioListener>();
            float distance = Vector3.Distance(sourcePosition, listener.transform.position);
            return distance / speedOfSound;
        }

        public static IEnumerator CoFadeMixerGroup(AudioMixer audioMixer, string exposedParameter, float duration, float targetVolume, float maxVolume = 1.0f)
        {
            float currentTime = 0;
            audioMixer.GetFloat(exposedParameter, out float currentVolume);
            currentVolume = Mathf.Pow(10, currentVolume / 20);
            float targetValue = Mathf.Clamp(targetVolume, 0.0001f, maxVolume);
            while (currentTime < duration)
            {
                currentTime += Time.deltaTime;
                float newVolume = Mathf.Lerp(currentVolume, targetValue, currentTime / duration);
                audioMixer.SetFloat(exposedParameter, Mathf.Log10(newVolume) * 20);
                yield return null;
            }

            audioMixer.SetFloat(exposedParameter, Mathf.Log10(targetValue) * 20);
        }

        public static bool TryGetMixerGroupVolumeParameter(AudioMixer audioMixer, string exposedParameter, out float volume)
        {
            if (audioMixer.GetFloat(exposedParameter, out volume))
            {
                volume = Mathf.Pow(10, volume / 20);
                return true;
            }

            return false;
        }

        public static void SetMixerGroupVolumeParameter(AudioMixer audioMixer, string exposedParameter, float volume, float maxVolume = 1.0f)
        {
            volume = Mathf.Clamp(volume, 0.0001f, maxVolume);
            audioMixer.SetFloat(exposedParameter, Mathf.Log10(volume) * 20);
        }
    }
}