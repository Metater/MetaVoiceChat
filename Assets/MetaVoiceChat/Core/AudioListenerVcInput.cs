using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace MetaVoiceChat.Core
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioListener))]
    public sealed class AudioListenerVcInput : MonoBehaviour
    {
        public static event Action<OnAudioFilterReadFrame> OnAudioFilterReadEvent;

        private static readonly List<AudioListenerVcInput> instances = new();
        private int cachedOutputSampleRate;

        public static int InstanceCount => instances.Count;

        private void OnEnable()
        {
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            CacheAudioSettings();
            instances.Add(this);

            if (instances.Count > 1)
            {
                Debug.LogError(
                    $"Multiple {nameof(AudioListenerVcInput)} instances found. This is not supported because Unity only allows one {nameof(AudioListener)}. This breaks acoustic echo cancellation.",
                    this);
            }
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            instances.Remove(this);
        }

        private void Update()
        {
            CacheAudioSettings();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null || channels <= 0)
            {
                return;
            }

            OnAudioFilterReadEvent?.Invoke(new OnAudioFilterReadFrame(
                data,
                data.Length,
                Volatile.Read(ref cachedOutputSampleRate),
                channels));
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            CacheAudioSettings();
        }

        private void CacheAudioSettings()
        {
            Volatile.Write(ref cachedOutputSampleRate, AudioSettings.outputSampleRate);
        }

        public readonly struct OnAudioFilterReadFrame
        {
            public readonly float[] data;
            public readonly int dataLength;
            public readonly int sampleRateHz;
            public readonly int channels;

            public OnAudioFilterReadFrame(float[] data, int dataLength, int sampleRateHz, int channels)
            {
                this.data = data;
                this.dataLength = dataLength;
                this.sampleRateHz = sampleRateHz;
                this.channels = channels;
            }
        }
    }
}
