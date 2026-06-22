using System;

namespace MetaVoiceChat.Core.AEC3
{
    /// <summary>Applies the native WebRTC high-pass filter to capture frames.</summary>
    public class HighPassFilterVcProcessor : IVcProcessor, IDisposable
    {
        private readonly float[] samples = new float[MetaVoiceChatConstants.MaxPossibleFrameSizeInSamples];

        private Aec3Interop.HighPassFilter filter;
        private int configuredFrequency;
        private int configuredChannels;
        private int lastSamplesProcessed;
        private bool disposed;

        /// <summary>
        /// Filtered samples from the most recent <see cref="Process"/> call.
        /// The returned span is backed by an internal buffer and must be read immediately.
        /// </summary>
        public ReadOnlySpan<float> FilteredSamples => samples.AsSpan(0, lastSamplesProcessed);

        /// <summary>Alias for <see cref="FilteredSamples"/>.</summary>
        public ReadOnlySpan<float> HighPassFilteredSamples => FilteredSamples;

        public void Process(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {
            ThrowIfDisposed();
            _ = sequenceNumber;
            _ = timestamp;

            CopyFrame(frame, frameSize, samples, nameof(HighPassFilterVcProcessor));
            if (frameSize == 0)
            {
                lastSamplesProcessed = 0;
                return;
            }

            EnsureFilter(frequency, channels);
            filter.ProcessInPlace(samples, frameSize);
            lastSamplesProcessed = frameSize;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            filter?.Dispose();
            filter = null;
            lastSamplesProcessed = 0;
        }

        private void EnsureFilter(int frequency, int channels)
        {
            if (filter != null && configuredFrequency == frequency && configuredChannels == channels)
            {
                return;
            }

            filter?.Dispose();

            Aec3Interop.HighPassConfig config = Aec3Interop.CreateDefaultHighPassConfig();
            config.SampleRateHz = frequency;
            config.Channels = channels;
            filter = new Aec3Interop.HighPassFilter(config);
            configuredFrequency = frequency;
            configuredChannels = channels;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(HighPassFilterVcProcessor));
            }
        }

        internal static void CopyFrame(ReadOnlySpan<float> frame, int frameSize, float[] destination, string processorName)
        {
            if (frameSize < 0 || frameSize > destination.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(frameSize));
            }

            if (frameSize == 0)
            {
                return;
            }

            if (frame.IsEmpty)
            {
                Array.Clear(destination, 0, frameSize);
                return;
            }

            if (frame.Length < frameSize)
            {
                throw new ArgumentException($"{processorName} frame length must be at least frameSize when data is present.", nameof(frame));
            }

            frame.Slice(0, frameSize).CopyTo(destination);
        }
    }
}
