using System;

namespace MetaVoiceChat.Core.AEC3
{
    /// <summary>Applies the native WebRTC AGC2 processor to capture frames.</summary>
    public class AutomaticGainControl2VcProcessor : IVcProcessor, IDisposable
    {
        private readonly float[] inputSamples = new float[MetaVoiceChatConstants.MaxPossibleFrameSizeInSamples];
        private readonly float[] gainControlledSamples = new float[MetaVoiceChatConstants.MaxPossibleFrameSizeInSamples];

        private Aec3Interop.Agc2 agc2;
        private int configuredFrequency;
        private int configuredChannels;
        private int lastSamplesProcessed;
        private bool disposed;

        /// <summary>
        /// The current microphone input-volume value supplied to AGC2. Unity does not expose a
        /// hardware microphone volume, so the default is the native maximum of 255.
        /// </summary>
        public int AppliedInputVolume { get; set; } = 255;

        /// <summary>
        /// Gain-controlled samples from the most recent <see cref="Process"/> call.
        /// The returned span is backed by an internal buffer and must be read immediately.
        /// </summary>
        public ReadOnlySpan<float> GainControlledSamples => gainControlledSamples.AsSpan(0, lastSamplesProcessed);

        /// <summary>Alias for <see cref="GainControlledSamples"/>.</summary>
        public ReadOnlySpan<float> ProcessedSamples => GainControlledSamples;

        public void Process(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {
            ThrowIfDisposed();
            _ = sequenceNumber;
            _ = timestamp;

            HighPassFilterVcProcessor.CopyFrame(frame, frameSize, inputSamples, nameof(AutomaticGainControl2VcProcessor));
            if (frameSize == 0)
            {
                lastSamplesProcessed = 0;
                return;
            }

            if (AppliedInputVolume < 0 || AppliedInputVolume > 255)
            {
                throw new ArgumentOutOfRangeException(nameof(AppliedInputVolume), "AGC2 input volume must be between 0 and 255.");
            }

            EnsureAgc2(frequency, channels);
            agc2.Process(inputSamples, frameSize, gainControlledSamples, frameSize, AppliedInputVolume);
            lastSamplesProcessed = frameSize;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            agc2?.Dispose();
            agc2 = null;
            lastSamplesProcessed = 0;
        }

        private void EnsureAgc2(int frequency, int channels)
        {
            if (agc2 != null && configuredFrequency == frequency && configuredChannels == channels)
            {
                return;
            }

            agc2?.Dispose();

            Aec3Interop.Agc2Config config = Aec3Interop.CreateDefaultAgc2Config();
            config.SampleRateHz = frequency;
            config.Channels = channels;
            agc2 = new Aec3Interop.Agc2(config);
            configuredFrequency = frequency;
            configuredChannels = channels;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(AutomaticGainControl2VcProcessor));
            }
        }
    }
}
