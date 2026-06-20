using System;
using UnityEngine;

namespace MetaVoiceChat.Core
{
    [CreateAssetMenu(fileName = "New OnAudioFilterRead VC Config", menuName = "MetaVoiceChat/OnAudioFilterReadVcConfig", order = 3)]
    public class OnAudioFilterReadVcConfig : ScriptableObject
    {
        [Header("Resampling Settings")]

        [Tooltip("Quality used by the software resampler when NetEQ's sample rate differs from Unity's output sample rate. 0 is fastest and lowest quality; 10 is slowest and highest quality. Values above 4 are usually not recommended for real-time voice.")]
        [Range(0, 10)]
        public int resamplerQuality = OnAudioFilterReadVcOutput.DefaultResamplerQuality;

        [Tooltip("Local output/resampler buffer target in milliseconds. NetEQ GetAudio is always pulled in 10 ms chunks; this value is rounded to the nearest 10 ms internally for buffering estimates.")]
        [Range(10, 100)]
        [SerializeField]
        private int resamplerBufferMs = OnAudioFilterReadVcOutput.DefaultResamplerBufferMs;

        [Header("NetEQ Settings")]

        [Tooltip("Maximum number of packets NetEQ may keep in its jitter buffer.")]
        [Range(5, 100)]
        public int maxPacketsInBuffer = OnAudioFilterReadVcOutput.DefaultMaxPacketsInBuffer;

        [Tooltip("Extra fixed delay added on top of NetEQ's adaptive jitter buffer. Leave at 0 for lowest latency; increase only if you intentionally want more buffering for stability or synchronization.")]
        [Range(0, 200)]
        public uint additionalDelayMs = OnAudioFilterReadVcOutput.DefaultAdditionalDelayMs;

        [Tooltip("")]
        public JitterBufferMode jitterBufferMode = OnAudioFilterReadVcOutput.DefaultJitterBufferMode;

        [Header("Custom NetEQ Delay Settings")]

        [Tooltip("Maximum adaptive jitter-buffer delay in milliseconds. Lower values feel more responsive; higher values tolerate shakier network timing at the cost of possible added latency.")]
        [Range(40, 300)]
        public uint maxDelayMs = OnAudioFilterReadVcOutput.DefaultMaxDelayMs;

        [Tooltip("Minimum adaptive jitter-buffer delay in milliseconds. Higher values can make playback steadier but add baseline latency. Keep this at or below Max Delay; the runtime should clamp Max Delay upward if needed.")]
        [Range(0, 100)]
        public uint minDelayMs = OnAudioFilterReadVcOutput.DefaultMinDelayMs;

        public int ResamplerBufferMs
        {
            get
            {
                int value = (resamplerBufferMs + 5) / 10 * 10;
                return Math.Clamp(value, 10, 100);
            }
        }

        [Serializable]
        public enum JitterBufferMode
        {
            LowLatency,
            Balanced,
            Stable,
            Custom,
        }

        public static uint GetMaxDelayMs(int packetMs, JitterBufferMode mode, OnAudioFilterReadVcConfig config)
        {
            if (mode == JitterBufferMode.Custom && config != null)
            {
                return config.maxDelayMs;
            }

            return mode switch
            {
                JitterBufferMode.LowLatency => packetMs switch
                {
                    10 => 60,
                    20 => 80,
                    40 => 120,
                    _ => (uint)Math.Clamp(packetMs * 4, 60, 160)
                },

                JitterBufferMode.Stable => packetMs switch
                {
                    10 => 100,
                    20 => 140,
                    40 => 200,
                    _ => (uint)Math.Clamp(packetMs * 6, 100, 240)
                },

                JitterBufferMode.Balanced or _ => packetMs switch
                {
                    10 => 80,
                    20 => 100,
                    40 => 140,
                    _ => (uint)Math.Clamp(packetMs * 5, 80, 200)
                }
            };
        }
    }
}
