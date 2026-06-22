using Concentus.Enums;
using System;

namespace MetaVoiceChat.Core.Opus
{
    public readonly struct OpusConfig : IEquatable<OpusConfig>
    {
        public const int BitsPerSample = 16;

        public readonly int sampleRate;
        public readonly int numChannels;
        public readonly OpusApplication application;
        public readonly OpusBandwidth bandwidth;
        public readonly int complexity;
        public readonly OpusMode mode;
        public readonly OpusSignal signal;

        public OpusConfig(int sampleRate, int numChannels, OpusApplication application, OpusBandwidth bandwidth, int complexity, OpusMode mode, OpusSignal signal)
        {
            this.sampleRate = sampleRate;
            this.numChannels = numChannels;
            this.application = application;
            this.bandwidth = bandwidth;
            this.complexity = complexity;
            this.mode = mode;
            this.signal = signal;
        }

        public bool Equals(OpusConfig other)
        {
            return sampleRate == other.sampleRate && numChannels == other.numChannels && application == other.application && bandwidth == other.bandwidth && complexity == other.complexity && mode == other.mode && signal == other.signal;
        }

        public override bool Equals(object obj)
        {
            return obj is OpusConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(sampleRate, numChannels, application, bandwidth, complexity, mode, signal);
        }
    }
}
