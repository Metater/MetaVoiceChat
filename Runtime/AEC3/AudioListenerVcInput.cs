using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace MetaVoiceChat.Core.AEC3
{
    /// <summary>
    /// Publishes Unity's final listener mix for use as the native AEC3 render reference.
    /// Add this component beside the active AudioListener, normally on the main camera.
    /// </summary>
    [AddComponentMenu("MetaVoiceChat/AEC3 Audio Listener Input")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioListener))]
    public sealed class AudioListenerVcInput : MonoBehaviour
    {
        private static readonly List<AudioListenerVcInput> Instances =
            new List<AudioListenerVcInput>();

        private int cachedOutputSampleRate;

        /// <summary>Raised on Unity's audio thread with the final listener mix.</summary>
        public static event Action<OnAudioFilterReadFrame> OnAudioFilterReadEvent;

        /// <summary>Gets the number of enabled listener-input components.</summary>
        public static int InstanceCount
        {
            get { return Instances.Count; }
        }

        private void OnEnable()
        {
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            CacheAudioSettings();
            Instances.Add(this);

            if (Instances.Count > 1)
            {
                Debug.LogError(
                    $"Multiple {nameof(AudioListenerVcInput)} instances are enabled. " +
                    "AEC3 requires one final AudioListener render stream.",
                    this);
            }
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            Instances.Remove(this);
        }

        private void Update()
        {
            CacheAudioSettings();
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            CacheAudioSettings();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null || data.Length == 0 || channels <= 0)
            {
                return;
            }

            Action<OnAudioFilterReadFrame> handler = OnAudioFilterReadEvent;
            if (handler == null)
            {
                return;
            }

            handler(new OnAudioFilterReadFrame(
                data,
                data.Length,
                Volatile.Read(ref cachedOutputSampleRate),
                channels));
        }

        private void CacheAudioSettings()
        {
            Volatile.Write(ref cachedOutputSampleRate, AudioSettings.outputSampleRate);
        }

        /// <summary>Describes one interleaved listener-mix callback.</summary>
        public readonly struct OnAudioFilterReadFrame
        {
            public readonly float[] data;
            public readonly int dataLength;
            public readonly int sampleRateHz;
            public readonly int channels;

            public OnAudioFilterReadFrame(
                float[] data,
                int dataLength,
                int sampleRateHz,
                int channels)
            {
                this.data = data;
                this.dataLength = dataLength;
                this.sampleRateHz = sampleRateHz;
                this.channels = channels;
            }
        }
    }
}
