#if UNITY_EDITOR && META_VOICE_CHAT_TESTS
using NUnit.Framework;
using MetaVoiceChat.NetProviders;
using MetaVoiceChat.Opus;
using MetaVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using OutputComponent = MetaVoiceChat.Output.OnAudioFilterReadVcOutput.OnAudioFilterReadVcOutput;

namespace MetaVoiceChat.Tests.Editor
{
    public class OnAudioFilterReadVcOutputTests
    {
        private const int InputSampleRate = 48000;

        private GameObject gameObject;
        private MetaVc metaVc;
        private OutputComponent output;
        private OutputComponent.EditorTestAdapter adapter;
        private VcConfig voiceConfig;

        [SetUp]
        public void SetUp()
        {
            RequireWindowsX64();

            gameObject = new GameObject(nameof(OnAudioFilterReadVcOutputTests));
            gameObject.SetActive(false);
            gameObject.AddComponent<UnityEngine.AudioSource>();
            metaVc = gameObject.AddComponent<MetaVc>();
            output = gameObject.AddComponent<OutputComponent>();

            voiceConfig = new VcConfig();
            voiceConfig.Init();
            metaVc.config = voiceConfig;
            metaVc.audioOutput = output;
            metaVc.isDeafened = new MetaSerializableReactiveProperty<bool>();
            metaVc.isInputMuted = new MetaSerializableReactiveProperty<bool>();
            metaVc.isOutputMuted = new MetaSerializableReactiveProperty<bool>();
            metaVc.isSpeaking = new MetaSerializableReactiveProperty<bool>();
            output.metaVc = metaVc;

            adapter = output.CreateEditorTestAdapter();
            adapter.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            adapter?.Dispose();
            if (gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Process_RejectsStereoAndUnsupportedInputRatesBeforeQueueing()
        {
            float[] stereo = new float[1920];
            NotSupportedException stereoException = Assert.Throws<NotSupportedException>(() =>
                output.Process(stereo.AsSpan(), stereo.Length, InputSampleRate, 2, 0, 0));
            StringAssert.Contains("mono network audio", stereoException.Message);

            float[] mono = new float[882];
            NotSupportedException rateException = Assert.Throws<NotSupportedException>(() =>
                output.Process(mono.AsSpan(), mono.Length, 44100, 1, 0, 0));
            StringAssert.Contains("48000 Hz", rateException.Message);

            Assert.That(adapter.GetStateSnapshot().PendingFrames, Is.Zero);
        }

        [TestCase(10)]
        [TestCase(20)]
        [TestCase(40)]
        public void Process_AcceptsSupportedMonoPacketDurations(int frameDurationMs)
        {
            int frameSize = InputSampleRate * frameDurationMs / 1000;
            float[] frame = CreateSineFrame(frameSize, 440f, 0);

            output.Process(frame.AsSpan(), frameSize, InputSampleRate, 1, 12, (uint)(12 * frameSize));

            OutputComponent.PendingFrameSnapshot pending = adapter.GetOldestPendingFrame();
            Assert.That(pending.HasValue, Is.True);
            Assert.That(pending.SampleLength, Is.EqualTo(frameSize));
            Assert.That(pending.SampleRate, Is.EqualTo(InputSampleRate));
            Assert.That(pending.Channels, Is.EqualTo(1));
        }

        [Test]
        public void Process_DropsUnsupportedPacketDurationWithoutNativeUse()
        {
            const int unsupportedDurationMs = 15;
            int frameSize = InputSampleRate * unsupportedDurationMs / 1000;
            float[] frame = new float[frameSize];

            output.Process(frame.AsSpan(), frameSize, InputSampleRate, 1, 0, 0);

            OutputComponent.StateSnapshot state = adapter.GetStateSnapshot();
            Assert.That(state.PendingFrames, Is.Zero);
            Assert.That(state.NetEqPointer, Is.EqualTo(IntPtr.Zero));
        }

        [Test]
        public void ReceiveFrame_WiresMetaVoiceChatIndexTimestampAndSilence()
        {
            const int frameIndex = 7;
            float[] frame = CreateSineFrame(voiceConfig.samplesPerFrame, 440f, 0);

            adapter.InvokeReceiveFrame(frameIndex, frame);

            OutputComponent.PendingFrameSnapshot pending = adapter.GetOldestPendingFrame();
            Assert.That(pending.HasValue, Is.True);
            Assert.That(pending.SampleLength, Is.EqualTo(voiceConfig.samplesPerFrame));
            Assert.That(pending.SampleRate, Is.EqualTo(InputSampleRate));
            Assert.That(pending.Channels, Is.EqualTo(1));
            Assert.That(pending.SequenceNumber, Is.EqualTo((ushort)frameIndex));
            Assert.That(pending.Timestamp, Is.EqualTo((uint)(frameIndex * voiceConfig.samplesPerFrame)));
            Assert.That(pending.IsSilence, Is.False);
        }

        [Test]
        public void MetaVcReceiveFrame_DecodesAndSubmitsThroughNormalOutputPath()
        {
            metaVc.StartClient(new TestNetProvider(), isLocalPlayer: false, maxDataBytesPerPacket: 1275);
            try
            {
                using VcEncoder encoder = new VcEncoder(voiceConfig, 1275);
                float[] frame = CreateSineFrame(voiceConfig.samplesPerFrame, 440f, 0);
                ReadOnlySpan<byte> encoded = encoder.EncodeFrame(frame);

                metaVc.ReceiveFrame(9, timestamp: 0d, additionalLatency: 0f, data: encoded);

                OutputComponent.PendingFrameSnapshot pending = adapter.GetOldestPendingFrame();
                Assert.That(pending.HasValue, Is.True);
                Assert.That(pending.SampleLength, Is.EqualTo(voiceConfig.samplesPerFrame));
                Assert.That(pending.SequenceNumber, Is.EqualTo((ushort)9));
                Assert.That(pending.Timestamp, Is.EqualTo((uint)(9 * voiceConfig.samplesPerFrame)));
                Assert.That(pending.IsSilence, Is.False);
            }
            finally
            {
                metaVc.StopClient();
            }
        }

        [Test]
        public void MetaVcReceiveFrame_SubmitsSilenceThroughNormalOutputPath()
        {
            metaVc.StartClient(new TestNetProvider(), isLocalPlayer: false, maxDataBytesPerPacket: 1275);
            try
            {
                metaVc.ReceiveFrame(
                    11,
                    timestamp: 0d,
                    additionalLatency: 0f,
                    data: ReadOnlySpan<byte>.Empty);

                OutputComponent.PendingFrameSnapshot pending = adapter.GetOldestPendingFrame();
                Assert.That(pending.HasValue, Is.True);
                Assert.That(pending.SampleLength, Is.EqualTo(voiceConfig.samplesPerFrame));
                Assert.That(pending.SequenceNumber, Is.EqualTo((ushort)11));
                Assert.That(pending.Timestamp, Is.EqualTo((uint)(11 * voiceConfig.samplesPerFrame)));
                Assert.That(pending.IsSilence, Is.True);
            }
            finally
            {
                metaVc.StopClient();
            }
        }

        [TestCase(44100, 200f)]
        [TestCase(48000, 1000f)]
        [TestCase(96000, 200f)]
        public void EndToEndPlayback_PreservesPitchAcrossOutputRates(int outputSampleRate, float frequency)
        {
            const int outputChannels = 2;
            adapter.Initialize(outputSampleRate, outputChannels, resamplerBufferMs: 30);

            List<float> rendered = RenderSine(
                outputSampleRate,
                outputChannels,
                inputFrameDurationMs: 20,
                resamplerBufferMs: 30,
                frequency,
                durationMs: 3000);

            AssertFiniteAndBounded(rendered);
            double measuredFrequency = EstimateFrequency(rendered, outputSampleRate);
            Assert.That(measuredFrequency, Is.EqualTo(frequency).Within(frequency * 0.08),
                $"Expected approximately {frequency} Hz but measured {measuredFrequency:0.00} Hz.");
            Assert.That(adapter.GetStateSnapshot().LastAudioThreadException, Is.Null);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(8)]
        public void EndToEndPlayback_ExpandsMonoToEveryUnityOutputChannel(int outputChannels)
        {
            adapter.Initialize(InputSampleRate, outputChannels, resamplerBufferMs: 20);
            List<float> interleaved = RenderInterleavedSine(
                InputSampleRate,
                outputChannels,
                inputFrameDurationMs: 20,
                resamplerBufferMs: 20,
                frequency: 440f,
                durationMs: 1000);

            float peak = 0f;
            for (int frameIndex = 0; frameIndex < interleaved.Count / outputChannels; frameIndex++)
            {
                int baseIndex = frameIndex * outputChannels;
                float first = interleaved[baseIndex];
                peak = Math.Max(peak, Math.Abs(first));
                for (int channel = 1; channel < outputChannels; channel++)
                {
                    Assert.That(interleaved[baseIndex + channel], Is.EqualTo(first).Within(1e-6f));
                }
            }

            Assert.That(peak, Is.GreaterThan(0.01f));
            Assert.That(adapter.GetStateSnapshot().LastAudioThreadException, Is.Null);
        }

        [TestCase(10, 0)]
        [TestCase(30, 15)]
        [TestCase(100, 80)]
        public void ResamplerBufferMs_PrefetchesConfiguredNetEqBatch(int bufferMs, int minimumExpectedBufferedMs)
        {
            const int channels = 2;
            adapter.Initialize(InputSampleRate, channels, bufferMs);
            int packetsToQueue = Math.Max(1, (bufferMs + 19) / 20);
            for (int packetIndex = 0; packetIndex < packetsToQueue; packetIndex++)
            {
                float[] frame = CreateSineFrame(960, 440f, packetIndex * 960);
                output.Process(frame.AsSpan(), frame.Length, InputSampleRate, 1, (ushort)packetIndex, (uint)(packetIndex * 960));
            }

            float[] data = CreateSpatializationMask(InputSampleRate / 100 * channels);
            adapter.InvokeAudioFilterRead(data, channels);

            OutputComponent.StateSnapshot state = adapter.GetStateSnapshot();
            Assert.That(state.LocalOutputBufferMs, Is.GreaterThanOrEqualTo(minimumExpectedBufferedMs));
            Assert.That(state.LocalOutputBufferMs, Is.LessThanOrEqualTo(bufferMs + 10));
            Assert.That(state.LastAudioThreadException, Is.Null);
        }

        [Test]
        public void PacketLossReorderingAndSilence_RemainFiniteAndRecover()
        {
            adapter.Initialize(InputSampleRate, 2, 20);
            int[] insertionOrder = { 0, 2, 1, 3, 4, 6, 5 };
            foreach (int packetIndex in insertionOrder)
            {
                Span<float> frame;
                float[] samples = null;
                if (packetIndex != 3)
                {
                    samples = CreateSineFrame(960, 300f, packetIndex * 960);
                    frame = samples.AsSpan();
                }
                else
                {
                    frame = Span<float>.Empty;
                }

                output.Process(frame, 960, InputSampleRate, 1, (ushort)packetIndex, (uint)(packetIndex * 960));
            }

            List<float> rendered = new List<float>();
            for (int callback = 0; callback < 20; callback++)
            {
                float[] data = CreateSpatializationMask(480 * 2);
                adapter.InvokeAudioFilterRead(data, 2);
                rendered.AddRange(data);
            }

            AssertFiniteAndBounded(rendered);
            Assert.That(adapter.GetStateSnapshot().LastAudioThreadException, Is.Null);
        }

        [Test]
        public void PendingFramePool_BoundsProducerBurstAndRecoversOnCallback()
        {
            for (int packetIndex = 0; packetIndex < 64; packetIndex++)
            {
                float[] frame = CreateSineFrame(480, 440f, packetIndex * 480);
                output.Process(frame.AsSpan(), frame.Length, InputSampleRate, 1, (ushort)packetIndex, (uint)(packetIndex * 480));
            }

            Assert.That(adapter.GetStateSnapshot().PendingFrames, Is.EqualTo(32));

            float[] data = CreateSpatializationMask(480);
            adapter.InvokeAudioFilterRead(data, 1);

            OutputComponent.StateSnapshot state = adapter.GetStateSnapshot();
            Assert.That(state.PendingFrames, Is.Zero);
            Assert.That(state.AvailableFrames, Is.EqualTo(32));
            Assert.That(state.LastAudioThreadException, Is.Null);
        }

        [Test]
        public void OutputFormatSwitch_ResetsConversionAndContinuesWithoutCorruption()
        {
            adapter.Initialize(InputSampleRate, 1, 20);
            FeedPackets(startPacket: 0, packetCount: 5, frameDurationMs: 20, frequency: 440f);
            for (int i = 0; i < 10; i++)
            {
                float[] data = CreateSpatializationMask(480);
                adapter.InvokeAudioFilterRead(data, 1);
            }

            adapter.SetOutputFormat(44100, 2);
            FeedPackets(startPacket: 5, packetCount: 5, frameDurationMs: 20, frequency: 440f);
            List<float> switchedOutput = new List<float>();
            for (int i = 0; i < 10; i++)
            {
                float[] data = CreateSpatializationMask(441 * 2);
                adapter.InvokeAudioFilterRead(data, 2);
                switchedOutput.AddRange(data);
            }

            AssertFiniteAndBounded(switchedOutput);
            Assert.That(adapter.GetStateSnapshot().LastAudioThreadException, Is.Null);
        }

        [Test]
        public void DisableDuringAdmittedCallback_DefersOneCleanupAndAllowsReactivation()
        {
            FeedPackets(startPacket: 0, packetCount: 2, frameDurationMs: 20, frequency: 440f);
            adapter.InvokeAudioFilterRead(CreateSpatializationMask(480), 1);
            OutputComponent.StateSnapshot before = adapter.GetStateSnapshot();
            Assert.That(before.NetEqPointer, Is.Not.EqualTo(IntPtr.Zero));

            using ManualResetEventSlim entered = new ManualResetEventSlim(false);
            using ManualResetEventSlim release = new ManualResetEventSlim(false);
            adapter.PauseNextAdmittedCallback(entered, release);
            float[] callbackData = CreateSpatializationMask(480);
            Task callback = Task.Run(() => adapter.InvokeAudioFilterRead(callbackData, 1));

            try
            {
                Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True, "The callback did not reach the test barrier.");
                Assert.That(adapter.GetStateSnapshot().CallbackDepth, Is.EqualTo(1));

                Stopwatch stopwatch = Stopwatch.StartNew();
                adapter.InvokeDisable();
                stopwatch.Stop();
                Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(500), "Disable blocked on the admitted callback.");

                OutputComponent.StateSnapshot deferred = adapter.GetStateSnapshot();
                Assert.That(deferred.CallbacksBlocked, Is.EqualTo(1));
                Assert.That(deferred.DisposeRequested, Is.EqualTo(1));
                Assert.That(deferred.NetEqPointer, Is.Not.EqualTo(IntPtr.Zero));

                adapter.RequestActivation();
                Assert.That(adapter.GetStateSnapshot().ActivateRequested, Is.EqualTo(1));
                Assert.That(adapter.GetStateSnapshot().OutputActive, Is.Zero);
            }
            finally
            {
                release.Set();
            }

            Assert.That(callback.Wait(TimeSpan.FromSeconds(5)), Is.True, "The admitted callback did not finish.");
            OutputComponent.StateSnapshot cleaned = adapter.GetStateSnapshot();
            Assert.That(cleaned.NetEqPointer, Is.EqualTo(IntPtr.Zero));
            Assert.That(cleaned.DisposeRequested, Is.Zero);
            Assert.That(cleaned.DisposeNotifications, Is.EqualTo(before.DisposeNotifications + 1));

            adapter.PumpDeferredLifecycle();
            OutputComponent.StateSnapshot reactivated = adapter.GetStateSnapshot();
            Assert.That(reactivated.OutputActive, Is.EqualTo(1));
            Assert.That(reactivated.CallbacksBlocked, Is.Zero);
            Assert.That(reactivated.ActivateRequested, Is.Zero);
        }

        [Test]
        public void DestroyDuringAdmittedCallback_DefersOneCleanupWithoutBlocking()
        {
            FeedPackets(startPacket: 0, packetCount: 2, frameDurationMs: 20, frequency: 440f);
            adapter.InvokeAudioFilterRead(CreateSpatializationMask(480), 1);
            OutputComponent.StateSnapshot before = adapter.GetStateSnapshot();
            Assert.That(before.NetEqPointer, Is.Not.EqualTo(IntPtr.Zero));

            using ManualResetEventSlim entered = new ManualResetEventSlim(false);
            using ManualResetEventSlim release = new ManualResetEventSlim(false);
            adapter.PauseNextAdmittedCallback(entered, release);
            Task callback = Task.Run(() =>
                adapter.InvokeAudioFilterRead(CreateSpatializationMask(480), 1));

            try
            {
                Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True, "The callback did not reach the test barrier.");

                Stopwatch stopwatch = Stopwatch.StartNew();
                adapter.InvokeDestroy();
                stopwatch.Stop();
                Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(500), "Destroy blocked on the admitted callback.");

                OutputComponent.StateSnapshot deferred = adapter.GetStateSnapshot();
                Assert.That(deferred.CallbacksBlocked, Is.EqualTo(1));
                Assert.That(deferred.DisposeRequested, Is.EqualTo(1));
                Assert.That(deferred.NetEqPointer, Is.Not.EqualTo(IntPtr.Zero));
            }
            finally
            {
                release.Set();
            }

            Assert.That(callback.Wait(TimeSpan.FromSeconds(5)), Is.True, "The admitted callback did not finish.");
            OutputComponent.StateSnapshot cleaned = adapter.GetStateSnapshot();
            Assert.That(cleaned.NetEqPointer, Is.EqualTo(IntPtr.Zero));
            Assert.That(cleaned.DisposeRequested, Is.Zero);
            Assert.That(cleaned.OutputActive, Is.Zero);
            Assert.That(cleaned.DisposeNotifications, Is.EqualTo(before.DisposeNotifications + 1));
        }

        [Test]
        public void AudioCallbackWithoutNativeState_OutputsSilenceWithoutFailure()
        {
            float[] data = CreateSpatializationMask(480 * 2);

            adapter.InvokeAudioFilterRead(data, 2);

            Assert.That(data, Is.All.EqualTo(0f));
            OutputComponent.StateSnapshot state = adapter.GetStateSnapshot();
            Assert.That(state.NetEqPointer, Is.EqualTo(IntPtr.Zero));
            Assert.That(state.LastAudioThreadException, Is.Null);
        }

        [Test]
        public void DestroyWhenIdle_FreesNativeStateImmediately()
        {
            FeedPackets(startPacket: 0, packetCount: 2, frameDurationMs: 20, frequency: 440f);
            adapter.InvokeAudioFilterRead(CreateSpatializationMask(480), 1);
            Assert.That(adapter.GetStateSnapshot().NetEqPointer, Is.Not.EqualTo(IntPtr.Zero));

            adapter.InvokeDestroy();

            OutputComponent.StateSnapshot state = adapter.GetStateSnapshot();
            Assert.That(state.NetEqPointer, Is.EqualTo(IntPtr.Zero));
            Assert.That(state.CallbacksBlocked, Is.EqualTo(1));
            Assert.That(state.DisposeRequested, Is.Zero);
        }

        [Test]
        public void WarmAudioCallbacks_DoNotAllocateLargeManagedBuffersRepeatedly()
        {
            adapter.Initialize(InputSampleRate, 2, 20);
            FeedPackets(startPacket: 0, packetCount: 10, frameDurationMs: 20, frequency: 440f);
            float[] data = CreateSpatializationMask(480 * 2);

            for (int i = 0; i < 10; i++)
            {
                FillWithOnes(data);
                adapter.InvokeAudioFilterRead(data, 2);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                FillWithOnes(data);
                adapter.InvokeAudioFilterRead(data, 2);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.LessThan(512 * 1024),
                $"Warm callbacks allocated {allocated} bytes; recurring audio buffers may be allocating.");
            Assert.That(adapter.GetStateSnapshot().LastAudioThreadException, Is.Null);
        }

        private List<float> RenderSine(
            int outputSampleRate,
            int outputChannels,
            int inputFrameDurationMs,
            int resamplerBufferMs,
            float frequency,
            int durationMs)
        {
            List<float> interleaved = RenderInterleavedSine(
                outputSampleRate,
                outputChannels,
                inputFrameDurationMs,
                resamplerBufferMs,
                frequency,
                durationMs);
            List<float> mono = new List<float>(interleaved.Count / outputChannels);
            for (int i = 0; i < interleaved.Count; i += outputChannels)
            {
                mono.Add(interleaved[i]);
            }

            return mono;
        }

        private List<float> RenderInterleavedSine(
            int outputSampleRate,
            int outputChannels,
            int inputFrameDurationMs,
            int resamplerBufferMs,
            float frequency,
            int durationMs)
        {
            adapter.SetResamplerBufferMs(resamplerBufferMs);
            int packetSamples = InputSampleRate * inputFrameDurationMs / 1000;
            int totalPackets = durationMs / inputFrameDurationMs;
            int packetsPerGroup = Math.Max(1, (resamplerBufferMs + inputFrameDurationMs - 1) / inputFrameDurationMs);
            int outputFramesPerCallback = outputSampleRate / 100;
            List<float> rendered = new List<float>(durationMs * outputSampleRate / 1000 * outputChannels);

            int packetIndex = 0;
            while (packetIndex < totalPackets)
            {
                int groupPackets = Math.Min(packetsPerGroup, totalPackets - packetIndex);
                for (int groupIndex = 0; groupIndex < groupPackets; groupIndex++, packetIndex++)
                {
                    float[] frame = CreateSineFrame(packetSamples, frequency, packetIndex * packetSamples);
                    output.Process(
                        frame.AsSpan(),
                        packetSamples,
                        InputSampleRate,
                        1,
                        (ushort)packetIndex,
                        (uint)(packetIndex * packetSamples));
                }

                int callbacks = groupPackets * inputFrameDurationMs / 10;
                for (int callback = 0; callback < callbacks; callback++)
                {
                    float[] data = CreateSpatializationMask(outputFramesPerCallback * outputChannels);
                    adapter.InvokeAudioFilterRead(data, outputChannels);
                    rendered.AddRange(data);
                }
            }

            return rendered;
        }

        private void FeedPackets(int startPacket, int packetCount, int frameDurationMs, float frequency)
        {
            int frameSize = InputSampleRate * frameDurationMs / 1000;
            for (int packetIndex = startPacket; packetIndex < startPacket + packetCount; packetIndex++)
            {
                float[] frame = CreateSineFrame(frameSize, frequency, packetIndex * frameSize);
                output.Process(
                    frame.AsSpan(),
                    frameSize,
                    InputSampleRate,
                    1,
                    (ushort)packetIndex,
                    (uint)(packetIndex * frameSize));
            }
        }

        private static float[] CreateSineFrame(int sampleCount, float frequency, int sampleOffset)
        {
            float[] frame = new float[sampleCount];
            for (int i = 0; i < frame.Length; i++)
            {
                frame[i] = 0.2f * Mathf.Sin(2f * Mathf.PI * frequency * (sampleOffset + i) / InputSampleRate);
            }

            return frame;
        }

        private static float[] CreateSpatializationMask(int sampleCount)
        {
            float[] data = new float[sampleCount];
            FillWithOnes(data);
            return data;
        }

        private static void FillWithOnes(float[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = 1f;
            }
        }

        private static void AssertFiniteAndBounded(IReadOnlyList<float> samples)
        {
            float peak = 0f;
            for (int i = 0; i < samples.Count; i++)
            {
                Assert.That(
                    !float.IsNaN(samples[i]) && !float.IsInfinity(samples[i]),
                    Is.True,
                    $"Sample {i} was not finite.");
                peak = Math.Max(peak, Math.Abs(samples[i]));
            }

            Assert.That(peak, Is.GreaterThan(0.01f));
            Assert.That(peak, Is.LessThanOrEqualTo(1.1f));
        }

        private static double EstimateFrequency(IReadOnlyList<float> samples, int sampleRate)
        {
            int firstSignal = -1;
            for (int i = 0; i < samples.Count; i++)
            {
                if (Math.Abs(samples[i]) > 0.01f)
                {
                    firstSignal = i;
                    break;
                }
            }

            Assert.That(firstSignal, Is.GreaterThanOrEqualTo(0), "No audible signal was rendered.");
            int start = Math.Min(samples.Count - 1, firstSignal + sampleRate / 4);
            int count = Math.Min(sampleRate, samples.Count - start);
            Assert.That(count, Is.GreaterThan(sampleRate / 2), "Not enough steady-state audio was rendered.");

            double mean = 0d;
            for (int i = start; i < start + count; i++)
            {
                mean += samples[i];
            }
            mean /= count;

            int positiveCrossings = 0;
            double previous = samples[start] - mean;
            for (int i = start + 1; i < start + count; i++)
            {
                double current = samples[i] - mean;
                if (previous <= 0d && current > 0d)
                {
                    positiveCrossings++;
                }
                previous = current;
            }

            return positiveCrossings * sampleRate / (double)(count - 1);
        }

        private static void RequireWindowsX64()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor || IntPtr.Size != 8)
            {
                Assert.Ignore("The trusted NetEQ output tests require the Windows x64 editor.");
            }
        }

        private sealed class TestNetProvider : INetProvider
        {
            public bool IsLocalPlayerDeafened => false;

            public void RelayFrame(int index, double timestamp, ReadOnlySpan<byte> data)
            {
            }
        }
    }
}
#endif
