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

        [Tooltip("NetEQ read/resampler batch size in milliseconds. Smaller values reduce extra local output buffering; larger values reduce read/resampler call overhead. Values larger than Unity's audio callback size can add latency. Rounded to the nearest 10 ms internally.")]
        [Range(10, 100)]
        [SerializeField]
        private int resamplerBufferMs = OnAudioFilterReadVcOutput.DefaultResamplerBufferMs;

        [Header("NetEQ Settings")]

        [Tooltip("Maximum number of packets NetEQ may keep in its jitter buffer. Higher values tolerate bursty packet arrival but can allow more buffered voice. This is a packet count, not a millisecond value.")]
        [Range(4, 64)]
        public int maxPacketsInBuffer = OnAudioFilterReadVcOutput.DefaultMaxPacketsInBuffer;

        [Tooltip("Maximum adaptive jitter-buffer delay in milliseconds. Lower values feel more responsive; higher values tolerate shakier network timing at the cost of possible added latency.")]
        [Range(40, 300)]
        public uint maxDelayMs = OnAudioFilterReadVcOutput.DefaultMaxDelayMs;

        [Tooltip("Minimum adaptive jitter-buffer delay in milliseconds. Higher values can make playback steadier but add baseline latency. Keep this at or below Max Delay; the runtime should clamp Max Delay upward if needed.")]
        [Range(0, 100)]
        public uint minDelayMs = OnAudioFilterReadVcOutput.DefaultMinDelayMs;

        [Tooltip("Extra fixed delay added on top of NetEQ's adaptive jitter buffer. Leave at 0 for lowest latency; increase only if you intentionally want more buffering for stability or synchronization.")]
        [Range(0, 200)]
        public uint additionalDelayMs = OnAudioFilterReadVcOutput.DefaultAdditionalDelayMs;

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