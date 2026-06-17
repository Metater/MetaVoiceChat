using UnityEngine;

namespace MetaVoiceChat.Core
{
    [CreateAssetMenu(fileName = "New OnAudioFilterRead VC Config", menuName = "MetaVoiceChat/OnAudioFilterReadVcConfig", order = 3)]
    public class OnAudioFilterReadVcConfig : ScriptableObject
    {
        [Header("Resampling Settings")]
        [Tooltip("Quality of the resampler when resampling to the frequency of your audio DSP. 0 is the fastest/lowest quality, 10 is the slowest/highest quality. Values above 4 are not recommended for real-time applications.")]
        [Range(0, 10)] public int resamplerQuality = OnAudioFilterReadVcOutput.DefaultResamplerQuality;
        [Tooltip("The batch size of audio samples to resample. This doesn't effect latency unless it is more than your frame size. This is rounded to the nearest multiple of 10 internally.")]
        [Range(10, 100)]
        [SerializeField] private int resamplerBufferMs = OnAudioFilterReadVcOutput.DefaultResamplerBufferMs;

        [Header("NetEq Settings")]
        [Range(1, 256)] public int maxPacketsInBuffer = OnAudioFilterReadVcOutput.DefaultMaxPacketsInBuffer;
        [Range(0, 1000)] public uint maxDelayMs = OnAudioFilterReadVcOutput.DefaultMaxDelayMs;
        [Range(0, 1000)] public uint minDelayMs = OnAudioFilterReadVcOutput.DefaultMinDelayMs;
        [Range(0, 1000)] public uint additionalDelayMs = OnAudioFilterReadVcOutput.DefaultAdditionalDelayMs;

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