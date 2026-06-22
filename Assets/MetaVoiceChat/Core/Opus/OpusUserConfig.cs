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
        public readonly OpusApplication application;
        public readonly int complexity;
        public readonly bool overrideSignalType;
        public readonly OpusSignal signalType;
        public readonly bool overrideMaxBandwidth;
        public readonly OpusBandwidth maxBandwidth;
        public readonly bool overrideForceMode;
        public readonly OpusMode forceMode;
        public readonly bool overrideBandwidth;
        public readonly OpusBandwidth bandwidth;
        public readonly bool overrideLSBDepth;
        public readonly int lsbDepth;
        public readonly bool overrideForceChannels;
        public readonly int forceChannels;
        public readonly bool overrideBitrate;
        public readonly int bitrate;
        public readonly bool overrideUseDTX;
        public readonly bool useDTX;
        public readonly bool overrideUseInbandFEC;
        public readonly bool useInbandFEC;
        public readonly bool overridePacketLossPercent;
        public readonly int packetLossPercent;
        public readonly bool overrideUseVBR;
        public readonly bool useVBR;
        public readonly bool overrideUseConstrainedVBR;
        public readonly bool useConstrainedVBR;
        public readonly bool overrideExpertFrameDuration;
        public readonly OpusFramesize expertFrameDuration;
        public readonly bool overridePredictionDisabled;
        public readonly bool predictionDisabled;

        public OpusConfig GetConfig(int frequency, int channels)
        {
            return new(
                sampleRate: frequency,
                numChannels: channels,
                application: application,
                complexity: complexity,
                overrideSignalType: overrideSignalType,
                signalType: signalType,
                overrideMaxBandwidth: overrideMaxBandwidth,
                maxBandwidth: maxBandwidth,
                overrideForceMode: overrideForceMode,
                forceMode: forceMode,
                overrideBandwidth: overrideBandwidth,
                bandwidth: bandwidth,
                overrideLSBDepth: overrideLSBDepth,
                lsbDepth: lsbDepth,
                overrideForceChannels: overrideForceChannels,
                forceChannels: forceChannels,
                overrideBitrate: overrideBitrate,
                bitrate: bitrate,
                overrideUseDTX: overrideUseDTX,
                useDTX: useDTX,
                overrideUseInbandFEC: overrideUseInbandFEC,
                useInbandFEC: useInbandFEC,
                overridePacketLossPercent: overridePacketLossPercent,
                packetLossPercent: packetLossPercent,
                overrideUseVBR: overrideUseVBR,
                useVBR: useVBR,
                overrideUseConstrainedVBR: overrideUseConstrainedVBR,
                useConstrainedVBR: useConstrainedVBR,
                overrideExpertFrameDuration: overrideExpertFrameDuration,
                expertFrameDuration: expertFrameDuration,
                overridePredictionDisabled: overridePredictionDisabled,
                predictionDisabled: predictionDisabled
            );
        }

        public OpusUserConfig(
            int maxDataBytesPerPacket,
            OpusApplication application,
            int complexity,
            bool overrideSignalType,
            OpusSignal signalType,
            bool overrideMaxBandwidth,
            OpusBandwidth maxBandwidth,
            bool overrideForceMode,
            OpusMode forceMode,
            bool overrideBandwidth,
            OpusBandwidth bandwidth,
            bool overrideLSBDepth,
            int lsbDepth,
            bool overrideForceChannels,
            int forceChannels,
            bool overrideBitrate,
            int bitrate,
            bool overrideUseDTX,
            bool useDTX,
            bool overrideUseInbandFEC,
            bool useInbandFEC,
            bool overridePacketLossPercent,
            int packetLossPercent,
            bool overrideUseVBR,
            bool useVBR,
            bool overrideUseConstrainedVBR,
            bool useConstrainedVBR,
            bool overrideExpertFrameDuration,
            OpusFramesize expertFrameDuration,
            bool overridePredictionDisabled,
            bool predictionDisabled)
        {
            this.maxDataBytesPerPacket = Math.Clamp(maxDataBytesPerPacket, 100, MetaVoiceChatConstants.MaxPacketSize);
            this.application = application;
            this.complexity = Math.Clamp(complexity, 0, 10);
            this.overrideSignalType = overrideSignalType;
            this.signalType = signalType;
            this.overrideMaxBandwidth = overrideMaxBandwidth;
            this.maxBandwidth = maxBandwidth;
            this.overrideForceMode = overrideForceMode;
            this.forceMode = forceMode;
            this.overrideBandwidth = overrideBandwidth;
            this.bandwidth = bandwidth;
            this.overrideLSBDepth = overrideLSBDepth;
            this.lsbDepth = lsbDepth;
            this.overrideForceChannels = overrideForceChannels;
            this.forceChannels = forceChannels;
            this.overrideBitrate = overrideBitrate;
            this.bitrate = bitrate;
            this.overrideUseDTX = overrideUseDTX;
            this.useDTX = useDTX;
            this.overrideUseInbandFEC = overrideUseInbandFEC;
            this.useInbandFEC = useInbandFEC;
            this.overridePacketLossPercent = overridePacketLossPercent;
            this.packetLossPercent = packetLossPercent;
            this.overrideUseVBR = overrideUseVBR;
            this.useVBR = useVBR;
            this.overrideUseConstrainedVBR = overrideUseConstrainedVBR;
            this.useConstrainedVBR = useConstrainedVBR;
            this.overrideExpertFrameDuration = overrideExpertFrameDuration;
            this.expertFrameDuration = expertFrameDuration;
            this.overridePredictionDisabled = overridePredictionDisabled;
            this.predictionDisabled = predictionDisabled;
        }

        public bool Equals(OpusUserConfig other)
        {
            return maxDataBytesPerPacket == other.maxDataBytesPerPacket && application == other.application && complexity == other.complexity && overrideSignalType == other.overrideSignalType && signalType == other.signalType && overrideMaxBandwidth == other.overrideMaxBandwidth && maxBandwidth == other.maxBandwidth && overrideForceMode == other.overrideForceMode && forceMode == other.forceMode && overrideBandwidth == other.overrideBandwidth && bandwidth == other.bandwidth && overrideLSBDepth == other.overrideLSBDepth && lsbDepth == other.lsbDepth && overrideForceChannels == other.overrideForceChannels && forceChannels == other.forceChannels && overrideBitrate == other.overrideBitrate && bitrate == other.bitrate && overrideUseDTX == other.overrideUseDTX && useDTX == other.useDTX && overrideUseInbandFEC == other.overrideUseInbandFEC && useInbandFEC == other.useInbandFEC && overridePacketLossPercent == other.overridePacketLossPercent && packetLossPercent == other.packetLossPercent && overrideUseVBR == other.overrideUseVBR && useVBR == other.useVBR && overrideUseConstrainedVBR == other.overrideUseConstrainedVBR && useConstrainedVBR == other.useConstrainedVBR && overrideExpertFrameDuration == other.overrideExpertFrameDuration && expertFrameDuration == other.expertFrameDuration && overridePredictionDisabled == other.overridePredictionDisabled && predictionDisabled == other.predictionDisabled;
        }

        public override bool Equals(object obj)
        {
            return obj is OpusUserConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(maxDataBytesPerPacket);
            hash.Add(application);
            hash.Add(complexity);
            hash.Add(overrideSignalType);
            hash.Add(signalType);
            hash.Add(overrideMaxBandwidth);
            hash.Add(maxBandwidth);
            hash.Add(overrideForceMode);
            hash.Add(forceMode);
            hash.Add(overrideBandwidth);
            hash.Add(bandwidth);
            hash.Add(overrideLSBDepth);
            hash.Add(lsbDepth);
            hash.Add(overrideForceChannels);
            hash.Add(forceChannels);
            hash.Add(overrideBitrate);
            hash.Add(bitrate);
            hash.Add(overrideUseDTX);
            hash.Add(useDTX);
            hash.Add(overrideUseInbandFEC);
            hash.Add(useInbandFEC);
            hash.Add(overridePacketLossPercent);
            hash.Add(packetLossPercent);
            hash.Add(overrideUseVBR);
            hash.Add(useVBR);
            hash.Add(overrideUseConstrainedVBR);
            hash.Add(useConstrainedVBR);
            hash.Add(overrideExpertFrameDuration);
            hash.Add(expertFrameDuration);
            hash.Add(overridePredictionDisabled);
            hash.Add(predictionDisabled);
            return hash.ToHashCode();
        }
    }
}
