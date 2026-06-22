using Concentus.Enums;
using UnityEngine;

namespace MetaVoiceChat.Core.Opus
{
    [CreateAssetMenu(fileName = "New Opus User Config", menuName = "MetaVoiceChat/Opus User Config", order = 5)]
    public class OpusUserConfigScriptableObject : ScriptableObject
    {
        //[Header("User-Friendly Settings")]

        [Tooltip("Hints to the encoder which details to preserve in the input signal.")]
        public OpusApplication application = OpusApplication.OPUS_APPLICATION_VOIP;

        [Tooltip("Encoder complexity. Higher values improve quality at the cost of CPU time.")]
        [Range(0, 10)]
        public int complexity = 3;

        [Tooltip("Override the input signal-type hint.")]
        public bool overrideSignalType = false;

        [Tooltip("Hints whether the input is voice or music. This is not determined through signal analysis.")]
        public OpusSignal signalType = OpusSignal.OPUS_SIGNAL_AUTO;

        [Tooltip("Override the maximum encoder bandwidth.")]
        public bool overrideMaxBandwidth = false;

        [Tooltip("Maximum bandwidth used by the encoder.")]
        public OpusBandwidth maxBandwidth = OpusBandwidth.OPUS_BANDWIDTH_AUTO;

        //[Header("Advanced Settings (Do research before changing anything here!)")]

        [Tooltip("Override the encoder mode.")]
        public bool overrideForceMode = false;

        [Tooltip("Forces SILK, Hybrid, or CELT encoding. The encoder may ignore this based on the frame size and bitrate.")]
        public OpusMode forceMode = OpusMode.MODE_AUTO;

        [Tooltip("Override the preferred encoder bandwidth.")]
        public bool overrideBandwidth = false;

        [Tooltip("Preferred encoded bandwidth. This changes encoding cutoffs, not the input sample rate.")]
        public OpusBandwidth bandwidth = OpusBandwidth.OPUS_BANDWIDTH_AUTO;

        [Tooltip("Override the input signal bit depth.")]
        public bool overrideLSBDepth = true;

        [Tooltip("Bit resolution of the input signal. The encoder uses 16-bit internally, but this helps it choose bandwidth and cutoff values.")]
        [Range(8, 24)]
        public int lsbDepth = 16;

        [Tooltip("Override the maximum number of encoded channels.")]
        public bool overrideForceChannels = false;

        [Tooltip("Maximum number of channels to encode. Set to -1000 to allow Opus to choose automatically (the encoder default), or 1 to force a stereo input down to mono.")]
        public int forceChannels = -1000;

        [Tooltip("Override the encoder bitrate.")]
        public bool overrideBitrate = false;

        [Tooltip("Bitrate in bits per second. Set to -1000 to let Opus automatically select the bitrate (the encoder default). Valid values are between 6K (6144) and 510K (522240).")]
        public int bitrate = -1000;

        [Tooltip("Override Discontinuous Transmission (DTX).")]
        public bool overrideUseDTX = false;

        [Tooltip("Enables DTX, which reduces packet transmission during silence. Only available in SILK mode.")]
        public bool useDTX;

        [Tooltip("Override in-band Forward Error Correction (FEC).")]
        public bool overrideUseInbandFEC = false;

        [Tooltip("Enables in-band FEC, allowing some lost packets to be partially recovered from the following packet. Only available in SILK mode.")]
        public bool useInbandFEC;

        [Tooltip("Override the expected packet loss percentage.")]
        public bool overridePacketLossPercent = false;

        [Tooltip("Expected network packet loss percentage. Only applies when in-band FEC is enabled in SILK mode.")]
        [Range(0, 100)]
        public int packetLossPercent = 0;

        [Tooltip("Override Variable Bitrate (VBR).")]
        public bool overrideUseVBR = false;

        [Tooltip("Enables VBR, which generally improves audio quality with little effect on average bitrate.")]
        public bool useVBR = true;

        [Tooltip("Override constrained Variable Bitrate (VBR).")]
        public bool overrideUseConstrainedVBR = false;

        [Tooltip("Enables constrained VBR. This only applies in CELT mode at higher bitrates.")]
        public bool useConstrainedVBR = true;

        [Tooltip("Override the expert frame duration.")]
        public bool overrideExpertFrameDuration = false;

        [Tooltip("Forces a fixed encoded frame duration instead of allowing the encoder to choose one.")]
        public OpusFramesize expertFrameDuration = OpusFramesize.OPUS_FRAMESIZE_ARG;

        [Tooltip("Override whether SILK prediction is disabled.")]
        public bool overridePredictionDisabled = false;

        [Tooltip("Disables prediction in the SILK codec.")]
        public bool predictionDisabled;

        public OpusUserConfig ToOpusUserConfig(int maxDataBytesPerPacket)
        {
            return new OpusUserConfig(
                maxDataBytesPerPacket,
                application,
                complexity,
                overrideSignalType,
                signalType,
                overrideMaxBandwidth,
                maxBandwidth,
                overrideForceMode,
                forceMode,
                overrideBandwidth,
                bandwidth,
                overrideLSBDepth,
                lsbDepth,
                overrideForceChannels,
                forceChannels,
                overrideBitrate,
                bitrate,
                overrideUseDTX,
                useDTX,
                overrideUseInbandFEC,
                useInbandFEC,
                overridePacketLossPercent,
                packetLossPercent,
                overrideUseVBR,
                useVBR,
                overrideUseConstrainedVBR,
                useConstrainedVBR,
                overrideExpertFrameDuration,
                expertFrameDuration,
                overridePredictionDisabled,
                predictionDisabled);
        }
    }
}
