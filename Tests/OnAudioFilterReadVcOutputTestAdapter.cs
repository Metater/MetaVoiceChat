#if UNITY_EDITOR && META_VOICE_CHAT_TESTS
using System;
using System.Threading;
using UnityEngine;

namespace MetaVoiceChat.Output.OnAudioFilterReadVcOutput
{
    public sealed partial class OnAudioFilterReadVcOutput
    {
        private Action editorTestAudioCallbackAdmitted;
        private Exception editorTestLastAudioThreadException;
        private int editorTestDisposeNotificationCount;

        partial void OnEditorAudioCallbackAdmitted()
        {
            Volatile.Read(ref editorTestAudioCallbackAdmitted)?.Invoke();
        }

        partial void OnEditorAudioThreadException(Exception exception)
        {
            editorTestLastAudioThreadException = exception;
        }

        partial void OnEditorAudioThreadStateDisposed()
        {
            Interlocked.Increment(ref editorTestDisposeNotificationCount);
        }

        public EditorTestAdapter CreateEditorTestAdapter()
        {
            return new EditorTestAdapter(this);
        }

        public sealed class EditorTestAdapter : IDisposable
        {
            private readonly OnAudioFilterReadVcOutput output;

            internal EditorTestAdapter(OnAudioFilterReadVcOutput output)
            {
                this.output = output;
            }

            public void Initialize(int outputSampleRate = 48000, int outputChannels = 1, int resamplerBufferMs = 10)
            {
                output.DisposeAudioThreadState();
                output.InitializePendingFramePool();
                output.ClearPendingFrames();
                output.audioSource = output.GetComponent<UnityEngine.AudioSource>();
                ConfigureAudioSource(output.audioSource);

                Volatile.Write(ref output.cachedOutputSampleRate, outputSampleRate);
                Volatile.Write(ref output.cachedOutputChannels, outputChannels);
                Volatile.Write(ref output.cachedDspBufferLength, Math.Max(1, outputSampleRate / 100));
                Volatile.Write(ref output.cachedDspBufferCount, 1);
                Volatile.Write(ref output.cachedDspBufferMs, 10);
                Volatile.Write(ref output.cachedResamplerQuality, DefaultResamplerQuality);
                SetResamplerBufferMs(resamplerBufferMs);

                Volatile.Write(ref output.resetAudioThreadStateRequested, 0);
                Volatile.Write(ref output.resetOutputConversionRequested, 0);
                Volatile.Write(ref output.disposeAudioThreadStateRequested, 0);
                Volatile.Write(ref output.activateOutputRequested, 0);
                Volatile.Write(ref output.audioCallbacksBlocked, 0);
                Volatile.Write(ref output.outputActive, 1);
                Volatile.Write(ref output.acceptingFrames, 1);

                output.editorTestLastAudioThreadException = null;
                Volatile.Write(ref output.editorTestDisposeNotificationCount, 0);
            }

            public void SetResamplerBufferMs(int value)
            {
                int rounded = Math.Clamp((value + 5) / 10 * 10, 10, 100);
                Volatile.Write(ref output.cachedResamplerBufferMs, rounded);
            }

            public void SetOutputFormat(int sampleRate, int channels)
            {
                Volatile.Write(ref output.cachedOutputSampleRate, sampleRate);
                Volatile.Write(ref output.cachedOutputChannels, channels);
                Volatile.Write(ref output.resetOutputConversionRequested, 1);
            }

            public void InvokeAudioFilterRead(float[] data, int channels)
            {
                output.OnAudioFilterRead(data, channels);
            }

            public void InvokeReceiveFrame(int index, float[] samples, float targetLatency = 0f)
            {
                output.ReceiveFrame(index, samples, targetLatency);
            }

            public void PauseNextAdmittedCallback(ManualResetEventSlim entered, ManualResetEventSlim release)
            {
                Action hook = null;
                hook = () =>
                {
                    entered.Set();
                    release.Wait();
                    Interlocked.CompareExchange(ref output.editorTestAudioCallbackAdmitted, null, hook);
                };
                Interlocked.Exchange(ref output.editorTestAudioCallbackAdmitted, hook);
            }

            public void RequestDisposal()
            {
                output.RequestAudioThreadStateDisposal();
            }

            public void InvokeDisable()
            {
                output.OnDisable();
            }

            public void InvokeDestroy()
            {
                output.OnDestroy();
            }

            public void RequestActivation()
            {
                Volatile.Write(ref output.activateOutputRequested, 1);
                output.TryActivateOutput();
            }

            public void PumpDeferredLifecycle()
            {
                output.TryCompleteRequestedAudioThreadStateDisposal();
                output.TryActivateOutput();
            }

            public StateSnapshot GetStateSnapshot()
            {
                return new StateSnapshot(
                    output.netEqPtr,
                    output.pendingFrames.Count,
                    output.availableFrames.Count,
                    output.outputFifo.Count,
                    Volatile.Read(ref output.audioCallbackDepth),
                    Volatile.Read(ref output.audioCallbacksBlocked),
                    Volatile.Read(ref output.disposeAudioThreadStateRequested),
                    Volatile.Read(ref output.activateOutputRequested),
                    Volatile.Read(ref output.outputActive),
                    Volatile.Read(ref output.acceptingFrames),
                    Volatile.Read(ref output.localOutputBufferMs),
                    Volatile.Read(ref output.editorTestDisposeNotificationCount),
                    output.editorTestLastAudioThreadException);
            }

            public PendingFrameSnapshot GetOldestPendingFrame()
            {
                if (!output.pendingFrames.TryPeek(out PendingFrame frame))
                {
                    return default;
                }

                return new PendingFrameSnapshot(
                    true,
                    frame.SampleLength,
                    frame.SampleRate,
                    frame.Channels,
                    frame.SequenceNumber,
                    frame.Timestamp,
                    frame.IsSilence);
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref output.editorTestAudioCallbackAdmitted, null);
                Volatile.Write(ref output.activateOutputRequested, 0);
                output.RequestAudioThreadStateDisposal();
                output.ClearPendingFrames();

                if (output.audioSource != null && output.audioSource.clip == output.playbackClip)
                {
                    output.audioSource.Stop();
                    output.audioSource.clip = null;
                }

                if (output.playbackClip != null)
                {
                    UnityEngine.Object.DestroyImmediate(output.playbackClip);
                    output.playbackClip = null;
                }
            }
        }

        public readonly struct StateSnapshot
        {
            public readonly IntPtr NetEqPointer;
            public readonly int PendingFrames;
            public readonly int AvailableFrames;
            public readonly int OutputFifoSamples;
            public readonly int CallbackDepth;
            public readonly int CallbacksBlocked;
            public readonly int DisposeRequested;
            public readonly int ActivateRequested;
            public readonly int OutputActive;
            public readonly int AcceptingFrames;
            public readonly int LocalOutputBufferMs;
            public readonly int DisposeNotifications;
            public readonly Exception LastAudioThreadException;

            internal StateSnapshot(
                IntPtr netEqPointer,
                int pendingFrames,
                int availableFrames,
                int outputFifoSamples,
                int callbackDepth,
                int callbacksBlocked,
                int disposeRequested,
                int activateRequested,
                int outputActive,
                int acceptingFrames,
                int localOutputBufferMs,
                int disposeNotifications,
                Exception lastAudioThreadException)
            {
                NetEqPointer = netEqPointer;
                PendingFrames = pendingFrames;
                AvailableFrames = availableFrames;
                OutputFifoSamples = outputFifoSamples;
                CallbackDepth = callbackDepth;
                CallbacksBlocked = callbacksBlocked;
                DisposeRequested = disposeRequested;
                ActivateRequested = activateRequested;
                OutputActive = outputActive;
                AcceptingFrames = acceptingFrames;
                LocalOutputBufferMs = localOutputBufferMs;
                DisposeNotifications = disposeNotifications;
                LastAudioThreadException = lastAudioThreadException;
            }
        }

        public readonly struct PendingFrameSnapshot
        {
            public readonly bool HasValue;
            public readonly int SampleLength;
            public readonly int SampleRate;
            public readonly int Channels;
            public readonly ushort SequenceNumber;
            public readonly uint Timestamp;
            public readonly bool IsSilence;

            internal PendingFrameSnapshot(
                bool hasValue,
                int sampleLength,
                int sampleRate,
                int channels,
                ushort sequenceNumber,
                uint timestamp,
                bool isSilence)
            {
                HasValue = hasValue;
                SampleLength = sampleLength;
                SampleRate = sampleRate;
                Channels = channels;
                SequenceNumber = sequenceNumber;
                Timestamp = timestamp;
                IsSilence = isSilence;
            }
        }
    }
}
#endif
