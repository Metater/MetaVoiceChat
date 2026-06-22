using System;

namespace MetaVoiceChat.Core.AEC3
{
    /// <summary>
    /// Applies native WebRTC AEC3 to capture frames and consumes the render reference published
    /// by <see cref="AudioListenerVcInput"/>. Attach that component to the scene's AudioListener.
    /// </summary>
    public class AcousticEchoCancellation3VcProcessor : IVcProcessor, IDisposable
    {
        private readonly object sync = new object();
        private readonly float[] captureSamples = new float[MetaVoiceChatConstants.MaxPossibleFrameSizeInSamples];
        private readonly float[] echoCancelledSamples = new float[MetaVoiceChatConstants.MaxPossibleFrameSizeInSamples];

        private Aec3Interop.EchoCanceller echoCanceller;
        private float[] renderAccumulator = Array.Empty<float>();
        private float[] renderFrame = Array.Empty<float>();
        private int renderAccumulatorCount;
        private int latestRenderFrequency;
        private int latestRenderChannels;
        private int configuredFrequency;
        private int configuredCaptureChannels;
        private int configuredRenderChannels;
        private int lastSamplesProcessed;
        private bool disposed;

        public AcousticEchoCancellation3VcProcessor()
        {
            AudioListenerVcInput.OnAudioFilterReadEvent += OnRenderFrame;
        }

        /// <summary>
        /// Echo-cancelled samples from the most recent <see cref="Process"/> call.
        /// The returned span is backed by an internal buffer and must be read immediately.
        /// </summary>
        public ReadOnlySpan<float> EchoCancelledSamples => echoCancelledSamples.AsSpan(0, lastSamplesProcessed);

        /// <summary>Alias for <see cref="EchoCancelledSamples"/>.</summary>
        public ReadOnlySpan<float> ProcessedSamples => EchoCancelledSamples;

        /// <summary>Number of render frames the native AEC3 queue is waiting to consume.</summary>
        public int QueuedRenderFrames
        {
            get
            {
                lock (sync)
                {
                    return echoCanceller?.QueuedRenderFrames ?? 0;
                }
            }
        }

        /// <summary>Number of render frames dropped because the native AEC3 queue was full.</summary>
        public long DroppedRenderFrames
        {
            get
            {
                lock (sync)
                {
                    return echoCanceller?.DroppedRenderFrames ?? 0;
                }
            }
        }

        public void Process(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {
            _ = sequenceNumber;
            _ = timestamp;

            lock (sync)
            {
                ThrowIfDisposed();
                HighPassFilterVcProcessor.CopyFrame(frame, frameSize, captureSamples, nameof(AcousticEchoCancellation3VcProcessor));
                if (frameSize == 0)
                {
                    lastSamplesProcessed = 0;
                    return;
                }

                EnsureEchoCanceller(frequency, channels);
                echoCanceller.ProcessCapture(captureSamples, frameSize, echoCancelledSamples, frameSize);
                lastSamplesProcessed = frameSize;
            }
        }

        /// <summary>
        /// Supplies interleaved render audio when the render source is not an
        /// <see cref="AudioListenerVcInput"/>. The data is split into native 10-ms frames.
        /// </summary>
        public bool TrySubmitRender(ReadOnlySpan<float> render, int renderSize, int frequency, int channels)
        {
            lock (sync)
            {
                ThrowIfDisposed();
                if (renderSize < 0 || renderSize > render.Length || channels < 1 || channels > Aec3Interop.MaxRenderChannels)
                {
                    return false;
                }

                latestRenderFrequency = frequency;
                latestRenderChannels = channels;
                if (!CanAcceptRender(frequency, channels))
                {
                    renderAccumulatorCount = 0;
                    return false;
                }

                AppendRender(render.Slice(0, renderSize));
                return true;
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                AudioListenerVcInput.OnAudioFilterReadEvent -= OnRenderFrame;
                echoCanceller?.Dispose();
                echoCanceller = null;
                renderAccumulatorCount = 0;
                lastSamplesProcessed = 0;
            }
        }

        private void OnRenderFrame(AudioListenerVcInput.OnAudioFilterReadFrame frame)
        {
            lock (sync)
            {
                if (disposed || frame.data == null || frame.dataLength < 0 || frame.dataLength > frame.data.Length ||
                    frame.channels < 1 || frame.channels > Aec3Interop.MaxRenderChannels)
                {
                    return;
                }

                latestRenderFrequency = frame.sampleRateHz;
                latestRenderChannels = frame.channels;
                if (!CanAcceptRender(frame.sampleRateHz, frame.channels))
                {
                    renderAccumulatorCount = 0;
                    return;
                }

                AppendRender(frame.data.AsSpan(0, frame.dataLength));
            }
        }

        private void EnsureEchoCanceller(int frequency, int captureChannels)
        {
            int renderChannels = latestRenderFrequency == frequency &&
                latestRenderChannels >= 1 && latestRenderChannels <= Aec3Interop.MaxRenderChannels
                ? latestRenderChannels
                : 1;

            if (echoCanceller != null && configuredFrequency == frequency &&
                configuredCaptureChannels == captureChannels && configuredRenderChannels == renderChannels)
            {
                return;
            }

            echoCanceller?.Dispose();

            Aec3Interop.AecConfig config = Aec3Interop.CreateDefaultAecConfig();
            config.SampleRateHz = frequency;
            config.CaptureChannels = captureChannels;
            config.RenderChannels = renderChannels;
            echoCanceller = new Aec3Interop.EchoCanceller(config);
            configuredFrequency = frequency;
            configuredCaptureChannels = captureChannels;
            configuredRenderChannels = renderChannels;
            renderAccumulator = new float[echoCanceller.RenderSamplesPer10Ms * 2];
            renderFrame = new float[echoCanceller.RenderSamplesPer10Ms];
            renderAccumulatorCount = 0;
        }

        private bool CanAcceptRender(int frequency, int channels)
        {
            return echoCanceller != null && configuredFrequency == frequency && configuredRenderChannels == channels;
        }

        private void AppendRender(ReadOnlySpan<float> samples)
        {
            int frameSize = echoCanceller.RenderSamplesPer10Ms;
            EnsureRenderCapacity(renderAccumulatorCount + samples.Length);
            samples.CopyTo(renderAccumulator.AsSpan(renderAccumulatorCount));
            renderAccumulatorCount += samples.Length;

            while (renderAccumulatorCount >= frameSize)
            {
                Array.Copy(renderAccumulator, 0, renderFrame, 0, frameSize);
                echoCanceller.TryEnqueueRenderFrame(renderFrame, frameSize);

                int remaining = renderAccumulatorCount - frameSize;
                if (remaining > 0)
                {
                    Array.Copy(renderAccumulator, frameSize, renderAccumulator, 0, remaining);
                }
                renderAccumulatorCount = remaining;
            }
        }

        private void EnsureRenderCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= renderAccumulator.Length)
            {
                return;
            }

            int capacity = renderAccumulator.Length == 0 ? requiredCapacity : renderAccumulator.Length;
            while (capacity < requiredCapacity)
            {
                capacity *= 2;
            }
            Array.Resize(ref renderAccumulator, capacity);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(AcousticEchoCancellation3VcProcessor));
            }
        }
    }
}
