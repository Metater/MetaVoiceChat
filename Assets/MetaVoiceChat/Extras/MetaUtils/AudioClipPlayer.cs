using System.Collections.Generic;
using UnityEngine;

namespace Metater
{
    public class AudioClipPlayer : MonoBehaviour
    {
        public AudioSource audioSource;
        public List<AudioClip> clips;
        public bool warningsEnabled = true;

        public int RandomAudioClipIndex => Random.Range(0, clips.Count);

        private AudioClip RandomAudioClip => clips[RandomAudioClipIndex];
        private int nextIndex = 0;

        public void PlayRandom(float volumeScale)
        {
            if (EmptyCheck())
            {
                return;
            }

            audioSource.PlayOneShot(RandomAudioClip, volumeScale);
        }

        //public void PlayLerp(float t, float volumeScale)
        //{
        //    if (EmptyCheck())
        //    {
        //        return;
        //    }

        //    // May not provide an even distribution
        //    int i = Mathf.RoundToInt(clips.Count * t);
        //    i = Mathf.Clamp(i, 0, clips.Count);
        //    audioSource.PlayOneShot(clips[i], volumeScale);
        //}

        public void PlayCyclical(float volumeScale)
        {
            if (EmptyCheck())
            {
                return;
            }

            int i = nextIndex++;
            if (i >= clips.Count)
            {
                i = 0;
                nextIndex = 1;
            }

            audioSource.PlayOneShot(clips[i], volumeScale);
        }

        public void PlayAtIndex(int i, float volumeScale)
        {
            if (EmptyCheck() || BoundsCheck(i))
            {
                return;
            }

            audioSource.PlayOneShot(clips[i], volumeScale);
        }

        private bool EmptyCheck()
        {
            if (clips.Count == 0)
            {
                if (warningsEnabled)
                {
                    Debug.LogWarning($"No audio clips on game object with name \"{gameObject.name}\".", gameObject);
                }

                return true;
            }

            return false;
        }

        private bool BoundsCheck(int i)
        {
            if (i >= clips.Count || i < 0)
            {
                if (warningsEnabled)
                {
                    Debug.LogWarning($"Provided audio clip index \"{i}\" is invalid on game object with name \"{gameObject.name}\".", gameObject);
                }

                return true;
            }

            return false;
        }

        public AudioClip GetAudioClipAtIndex(int i)
        {
            if (BoundsCheck(i))
            {
                return null;
            }

            return clips[i];
        }
    }
}
