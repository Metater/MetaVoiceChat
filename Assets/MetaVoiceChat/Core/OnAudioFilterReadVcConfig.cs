using UnityEngine;

namespace MetaVoiceChat.Core
{
    [CreateAssetMenu(fileName = "New OnAudioFilterRead VC Config", menuName = "MetaVoiceChat/OnAudioFilterReadVcConfig", order = 3)]
    public class OnAudioFilterReadVcConfig : ScriptableObject
    {
        [Header("Resampling Settings")]
        [Tooltip("Quality used when resampling NetEQ output to Unity's audio DSP rate. 0 is fastest and lowest quality; 10 is slowest and highest quality. Values above 4 are not recommended for real-time voice.")]
        [Range(0, 10)] public int resamplerQuality = OnAudioFilterReadVcOutput.DefaultResamplerQuality;
        [Tooltip("NetEQ read and resampling batch size in milliseconds. Smaller values can reduce local output buffering; larger values can reduce call overhead. Rounded to the nearest 10 ms internally.")]
        [Range(10, 100)]
        [SerializeField] private int resamplerBufferMs = OnAudioFilterReadVcOutput.DefaultResamplerBufferMs;

        [Header("NetEq Settings")]
        [Tooltip("Maximum number of packets NetEQ may keep in its jitter buffer. Higher values tolerate bursty network timing but can allow more buffered voice.")]
        [Range(4, 64)] public int maxPacketsInBuffer = OnAudioFilterReadVcOutput.DefaultMaxPacketsInBuffer;
        [Tooltip("Maximum target jitter-buffer delay in milliseconds. Lower values feel more responsive; higher values are more tolerant of shaky network conditions.")]
        [Range(0, 300)] public uint maxDelayMs = OnAudioFilterReadVcOutput.DefaultMaxDelayMs;
        [Tooltip("Minimum target jitter-buffer delay in milliseconds. Keep this at or below Max Delay; the runtime clamps Max Delay upward if needed.")]
        [Range(0, 100)] public uint minDelayMs = OnAudioFilterReadVcOutput.DefaultMinDelayMs;
        [Tooltip("Extra fixed delay added on top of NetEQ's adaptive jitter buffer. Leave at 0 unless you intentionally want more latency for stability or synchronization.")]
        [Range(0, 200)] public uint additionalDelayMs = OnAudioFilterReadVcOutput.DefaultAdditionalDelayMs;

        public int ResamplerBufferMs
        {
            get
            {
                int value = (resamplerBufferMs + 5) / 10 * 10;
                return System.Math.Clamp(value, 10, 100);
            }
        }
    }
}
