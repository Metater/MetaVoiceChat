using MetaVoiceChat.Native;
using UnityEngine;

namespace MetaVoiceChat.Output.OnAudioFilterReadVcOutput
{
    /// <summary>Inspector settings for the native microphone loopback.</summary>
    [CreateAssetMenu(
        fileName = "New Native Loopback Config",
        menuName = "MetaVoiceChat/Native Loopback Config",
        order = 3)]
    public sealed class OnAudioFilterReadVcConfig : ScriptableObject
    {
        public enum PacketDuration : uint
        {
            Ms10 = 10,
            Ms20 = 20,
            Ms40 = 40,
        }

        [Header("Packet Loopback")]
        public PacketDuration packetDuration = PacketDuration.Ms20;

        [Tooltip("Estimated speaker-to-microphone delay supplied to AEC3.")]
        [Range(0, 500)]
        public int renderDelayMs = 40;

        [Header("Microphone Processing")]
        public bool echoCancellation = true;
        public bool highPassFilter;
        public bool noiseSuppression = true;
        public bool automaticGainControl = true;

        internal MvcInterop.ProcessingFlags ProcessingFlags
        {
            get
            {
                MvcInterop.ProcessingFlags flags = MvcInterop.ProcessingFlags.None;
                if (echoCancellation)
                {
                    flags |= MvcInterop.ProcessingFlags.Aec3;
                }

                if (highPassFilter)
                {
                    flags |= MvcInterop.ProcessingFlags.HighPassFilter;
                }

                if (noiseSuppression)
                {
                    flags |= MvcInterop.ProcessingFlags.Rnnoise;
                }

                if (automaticGainControl)
                {
                    flags |= MvcInterop.ProcessingFlags.Agc2;
                }

                return flags;
            }
        }
    }
}
