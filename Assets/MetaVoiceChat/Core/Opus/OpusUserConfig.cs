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
        public readonly bool shouldOverrideApplication;
        public readonly OpusApplication overrideApplication;
        public readonly bool shouldOverrideBandwidth;
        public readonly OpusBandwidth overrideBandwidth;
        public readonly bool shouldOverrideMode;
        public readonly OpusMode overrideMode;
        public readonly bool shouldOverrideSignal;
        public readonly OpusSignal overrideSignal;

        public OpusMode GetMode()
        {
            if (shouldOverrideMode)
            {
                return overrideMode;
            }

            // There might have been a reason for this before, I think it switched to hybrid mode automatically and caused a crash with some config

            //if (isMusic)
            //{
            //    return OpusMode.MODE_CELT_ONLY;
            //}

            //return OpusMode.MODE_CELT_ONLY;

            return OpusMode.MODE_AUTO;
        }

        public OpusBandwidth GetBandwidth(int frequency, int channels)
        {
            if (shouldOverrideBandwidth)
            {
                return overrideBandwidth;
            }

            //if (channels > 1)
            //{
            //    return OpusBandwidth.OPUS_BANDWIDTH_FULLBAND;
            //}

            return OpusBandwidth.OPUS_BANDWIDTH_AUTO;
        }

        public OpusApplication GetApplication()
        {
            if (shouldOverrideApplication)
            {
                return overrideApplication;
            }

            return isMusic ? OpusApplication.OPUS_APPLICATION_AUDIO : OpusApplication.OPUS_APPLICATION_VOIP;
        }

        public OpusSignal GetSignal()
        {
            if (shouldOverrideSignal)
            {
                return overrideSignal;
            }

            //return isMusic ? OpusSignal.OPUS_SIGNAL_MUSIC : OpusSignal.OPUS_SIGNAL_VOICE;

            return OpusSignal.OPUS_SIGNAL_AUTO;
        }

        public OpusConfig GetConfig(int frequency, int channels)
        {
            return new(
                sampleRate: frequency,
                numChannels: channels,
                application: GetApplication(),
                bandwidth: GetBandwidth(frequency, channels),
                complexity: complexity,
                mode: GetMode(),
                signal: GetSignal()
            );
        }

        public OpusUserConfig(int maxDataBytesPerPacket, int complexity, bool isMusic, bool shouldOverrideApplication, OpusApplication overrideApplication, bool shouldOverrideBandwidth, OpusBandwidth overrideBandwidth, bool shouldOverrideMode, OpusMode overrideMode, bool shouldOverrideSignal, OpusSignal overrideSignal)
        {
            this.maxDataBytesPerPacket = Math.Clamp(maxDataBytesPerPacket, 100, MetaVoiceChatConstants.MaxPacketSize);
            this.complexity = Math.Clamp(complexity, 0, 10);
            this.isMusic = isMusic;
            this.shouldOverrideApplication = shouldOverrideApplication;
            this.overrideApplication = overrideApplication;
            this.shouldOverrideBandwidth = shouldOverrideBandwidth;
            this.overrideBandwidth = overrideBandwidth;
            this.shouldOverrideMode = shouldOverrideMode;
            this.overrideMode = overrideMode;
            this.shouldOverrideSignal = shouldOverrideSignal;
            this.overrideSignal = overrideSignal;
        }

        public bool Equals(OpusUserConfig other)
        {
            return maxDataBytesPerPacket == other.maxDataBytesPerPacket && complexity == other.complexity && isMusic == other.isMusic && shouldOverrideApplication == other.shouldOverrideApplication && overrideApplication == other.overrideApplication && shouldOverrideBandwidth == other.shouldOverrideBandwidth && overrideBandwidth == other.overrideBandwidth && shouldOverrideMode == other.shouldOverrideMode && overrideMode == other.overrideMode && shouldOverrideSignal == other.shouldOverrideSignal && overrideSignal == other.overrideSignal;
        }

        public override bool Equals(object obj)
        {
            return obj is OpusUserConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            int overrideBooleans = 0;
            overrideBooleans |= (shouldOverrideApplication ? 1 : 0) << 0;
            overrideBooleans |= (shouldOverrideBandwidth ? 1 : 0) << 1;
            overrideBooleans |= (shouldOverrideMode ? 1 : 0) << 2;
            overrideBooleans |= (shouldOverrideSignal ? 1 : 0) << 3;

            return HashCode.Combine(maxDataBytesPerPacket, complexity, isMusic, overrideApplication, overrideBandwidth, overrideMode, overrideSignal, overrideBooleans);
        }
    }
}
