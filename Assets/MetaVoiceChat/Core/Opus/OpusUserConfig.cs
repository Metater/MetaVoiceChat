using Concentus.Enums;
using System;

namespace MetaVoiceChat.Core.Opus
{
    public sealed class OpusUserConfigBox
    {
        public readonly OpusUserConfig value;

        public OpusUserConfigBox(OpusUserConfig value)
        {
            this.value = value;
        }
    }

    public readonly struct OpusUserConfig : IEquatable<OpusUserConfig>
    {
        public readonly int maxDataBytesPerPacket;
        public readonly int complexity;
        public readonly bool isMusic;
        public readonly bool shouldOverride;
        public readonly OpusApplication overrideApplication;
        public readonly OpusBandwidth overrideBandwidth;
        public readonly OpusMode overrideMode;
        public readonly OpusSignal overrideSignal;

        public OpusMode GetMode(int frequency)
        {
            if (shouldOverride)
            {
                return overrideMode;
            }

            if (isMusic)
            {
                return OpusMode.MODE_CELT_ONLY;
            }

            if (frequency == 8000 || frequency == 12000)
            {
                return OpusMode.MODE_SILK_ONLY;
            }

            return OpusMode.MODE_CELT_ONLY;
        }

        public OpusBandwidth GetBandwidth(int frequency)
        {
            if (shouldOverride)
            {
                return overrideBandwidth;
            }

            // Referenced: https://wiki.xiph.org/Opus_Recommended_Settings
            // Bandwidth Transition Thresholds
            // Note the Nyquist frequency is half the sampling rate
            return frequency switch
            {
                8000 => OpusBandwidth.OPUS_BANDWIDTH_NARROWBAND,
                12000 => OpusBandwidth.OPUS_BANDWIDTH_MEDIUMBAND,
                16000 => OpusBandwidth.OPUS_BANDWIDTH_WIDEBAND,
                24000 => OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND,
                48000 => OpusBandwidth.OPUS_BANDWIDTH_FULLBAND,
                _ => OpusBandwidth.OPUS_BANDWIDTH_FULLBAND,
            };
        }

        public OpusApplication GetApplication()
        {
            if (shouldOverride)
            {
                return overrideApplication;
            }

            return isMusic ? OpusApplication.OPUS_APPLICATION_AUDIO : OpusApplication.OPUS_APPLICATION_VOIP;
        }

        public OpusSignal GetSignal()
        {
            if (shouldOverride)
            {
                return overrideSignal;
            }

            return isMusic ? OpusSignal.OPUS_SIGNAL_MUSIC : OpusSignal.OPUS_SIGNAL_VOICE;
        }

        public OpusConfig GetConfig(int frequency, int channels)
        {
            return new(
                sampleRate: frequency,
                numChannels: channels,
                application: GetApplication(),
                bandwidth: GetBandwidth(frequency),
                complexity: complexity,
                mode: GetMode(frequency),
                signal: GetSignal()
            );
        }

        public OpusUserConfig(int maxDataBytesPerPacket, int complexity, bool isMusic, bool shouldOverride, OpusApplication overrideApplication, OpusBandwidth overrideBandwidth, OpusMode overrideMode, OpusSignal overrideSignal)
        {
            this.maxDataBytesPerPacket = Math.Clamp(maxDataBytesPerPacket, 100, OpusConfig.MaxPacketSize);
            this.complexity = Math.Clamp(complexity, 0, 10);
            this.isMusic = isMusic;
            this.shouldOverride = shouldOverride;
            this.overrideApplication = overrideApplication;
            this.overrideBandwidth = overrideBandwidth;
            this.overrideMode = overrideMode;
            this.overrideSignal = overrideSignal;
        }

        public bool Equals(OpusUserConfig other)
        {
            return maxDataBytesPerPacket == other.maxDataBytesPerPacket && complexity == other.complexity && isMusic == other.isMusic && shouldOverride == other.shouldOverride && overrideApplication == other.overrideApplication && overrideBandwidth == other.overrideBandwidth && overrideMode == other.overrideMode && overrideSignal == other.overrideSignal;
        }

        public override bool Equals(object obj)
        {
            return obj is OpusUserConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(maxDataBytesPerPacket, complexity, isMusic, shouldOverride, overrideApplication, overrideBandwidth, overrideMode, overrideSignal);
        }
    }
}
