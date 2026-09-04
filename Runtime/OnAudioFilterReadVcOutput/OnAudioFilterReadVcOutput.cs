using System;
using System.Threading;
using MetaVoiceChat.Core.AEC3;
using MetaVoiceChat.Native;
using UnityEngine;

namespace MetaVoiceChat.Output.OnAudioFilterReadVcOutput
{
    /// <summary>
    /// Inspector-ready default-microphone loopback through MetaVoiceChatNative.
    /// </summary>
    [AddComponentMenu("MetaVoiceChat/Native Microphone Loopback")]
    [RequireComponent(typeof(AudioSource))]
    public sealed class OnAudioFilterReadVcOutput : MonoBehaviour
    {
        private const int MaximumPacketsPerUpdate = 32;
        private const uint DefaultPacketDurationMs = 20;
        private const int DefaultRenderDelayMs = 40;
        private const int MinimumMaximumCallbackFrames = 4096;

        [SerializeField]
        private OnAudioFilterReadVcConfig audioFilterReadConfig;

        private readonly byte[] packetBytes = new byte[MvcInterop.MaxOpusPacketBytes];

        private AudioSource audioSource;
        private AudioClip playbackClip;
        private float[] playbackClipSamples;
        private MvcInterop.Engine engine;
        private MvcInterop.Packet pendingPacket;
        private uint voiceId;
        private int outputSampleRate;
        private int outputChannels;
        private int callbacksBlocked = 1;
        private int audioCallbackDepth;
        private int restartRequested;
        private int audioThreadFailure;
        private int cachedRenderDelayMs = DefaultRenderDelayMs;
        private int renderReferenceEnabled;
        private bool hasPendingPacket;
        private bool failureLogged;
        private bool missingListenerWarningLogged;

        private void OnEnable()
        {
            if (!IsSupportedPlatform())
            {
                Debug.LogError(
                    $"{nameof(OnAudioFilterReadVcOutput)} requires the Windows x64 " +
                    "MetaVoiceChatNative.dll plugin.",
                    this);
                enabled = false;
                return;
            }

            audioSource = GetComponent<AudioSource>();
            ConfigureAudioSource(audioSource);
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            AudioListenerVcInput.OnAudioFilterReadEvent += OnAudioListenerRender;
            StartLoopback();
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            AudioListenerVcInput.OnAudioFilterReadEvent -= OnAudioListenerRender;
            StopLoopback();
        }

        private void OnDestroy()
        {
            StopLoopback();
        }

        private void OnValidate()
        {
            if (TryGetComponent(out AudioSource source))
            {
                ConfigureAudioSource(source);
            }
        }

        private void Update()
        {
            if (Interlocked.Exchange(ref restartRequested, 0) != 0)
            {
                StopLoopback();
                StartLoopback();
            }

            PumpLoopbackPackets();

            if (Volatile.Read(ref renderReferenceEnabled) != 0 &&
                AudioListenerVcInput.InstanceCount == 0 &&
                !missingListenerWarningLogged)
            {
                missingListenerWarningLogged = true;
                Debug.LogWarning(
                    $"AEC3 is enabled, but no {nameof(AudioListenerVcInput)} is active. " +
                    "Add one to the main camera beside its AudioListener.",
                    this);
            }

            int failure = Interlocked.Exchange(ref audioThreadFailure, 0);
            if (failure < 0 && !failureLogged)
            {
                failureLogged = true;
                Debug.LogError(
                    $"Native Unity audio callback failed with {(MvcInterop.Result)failure}.",
                    this);
            }

            if (engine != null && audioSource != null && !audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }

        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            Interlocked.Exchange(ref restartRequested, 1);
        }

        private void StartLoopback()
        {
            if (!isActiveAndEnabled || engine != null)
            {
                return;
            }

            try
            {
                uint abiVersion = MvcInterop.GetAbiVersion();
                if (abiVersion != MvcInterop.ExpectedAbiVersion)
                {
                    Debug.LogError(
                        $"MetaVoiceChatNative ABI {abiVersion} was loaded, but ABI " +
                        $"{MvcInterop.ExpectedAbiVersion} is required.",
                        this);
                    enabled = false;
                    return;
                }

                int sampleRate = AudioSettings.outputSampleRate;
                outputSampleRate = sampleRate;
                outputChannels = GetSpeakerModeChannelCount(AudioSettings.speakerMode);
                AudioSettings.GetDSPBufferSize(out int dspBufferFrames, out _);
                int maximumCallbackFrames = Math.Max(MinimumMaximumCallbackFrames, dspBufferFrames);

                if (sampleRate <= 0 || outputChannels == 0)
                {
                    Debug.LogError("Unity reported an unsupported output audio format.", this);
                    enabled = false;
                    return;
                }

                MvcInterop.ProcessingFlags processingFlags = GetProcessingFlags();
                MvcInterop.Config config = new MvcInterop.Config
                {
                    CaptureSource = MvcInterop.CaptureSource.DefaultMicrophone,
                    CaptureInputChannels = 1,
                    UnityOutputSampleRateHz = (uint)sampleRate,
                    UnityOutputChannels = (uint)outputChannels,
                    MaximumCallbackFrames = (uint)maximumCallbackFrames,
                    OpusPacketDurationMs = GetPacketDurationMs(),
                    MaximumRemoteVoices = 1,
                    ProcessingFlags = processingFlags,
                };

                Volatile.Write(ref cachedRenderDelayMs, GetConfiguredRenderDelayMs());
                Volatile.Write(
                    ref renderReferenceEnabled,
                    (processingFlags & MvcInterop.ProcessingFlags.Aec3) != 0 ? 1 : 0);

                MvcInterop.Result result = MvcInterop.Create(config, out MvcInterop.Engine createdEngine);
                if (result != MvcInterop.Result.Ok)
                {
                    Debug.LogError(
                        $"Native engine creation failed with {result}. Is a default microphone available?",
                        this);
                    enabled = false;
                    return;
                }

                result = createdEngine.AddVoice(out uint createdVoiceId);
                if (result != MvcInterop.Result.Ok || createdVoiceId == 0)
                {
                    createdEngine.Dispose();
                    Debug.LogError($"Native voice creation failed with {result}.", this);
                    enabled = false;
                    return;
                }

                result = createdEngine.Start();
                if (result != MvcInterop.Result.Ok)
                {
                    createdEngine.Dispose();
                    Debug.LogError($"Native engine start failed with {result}.", this);
                    enabled = false;
                    return;
                }

                voiceId = createdVoiceId;
                engine = createdEngine;
                CreatePlaybackClip(sampleRate);
                hasPendingPacket = false;
                failureLogged = false;
                missingListenerWarningLogged = false;
                Interlocked.Exchange(ref audioThreadFailure, 0);
                Volatile.Write(ref callbacksBlocked, 0);
                audioSource.Play();

                Debug.Log($"Started {MvcInterop.GetVersion()} microphone loopback.", this);
            }
            catch (Exception exception)
            {
                StopLoopback();
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void StopLoopback()
        {
            Volatile.Write(ref callbacksBlocked, 1);

            if (audioSource != null)
            {
                audioSource.Stop();
            }

            while (Volatile.Read(ref audioCallbackDepth) != 0)
            {
                Thread.Yield();
            }

            MvcInterop.Engine activeEngine = engine;
            engine = null;
            voiceId = 0;
            Volatile.Write(ref renderReferenceEnabled, 0);
            hasPendingPacket = false;

            if (activeEngine != null)
            {
                activeEngine.Stop();
                activeEngine.Dispose();
            }

            if (audioSource != null && audioSource.clip == playbackClip)
            {
                audioSource.clip = null;
            }

            if (playbackClip != null)
            {
                Destroy(playbackClip);
                playbackClip = null;
            }

            playbackClipSamples = null;
        }

        private void PumpLoopbackPackets()
        {
            MvcInterop.Engine activeEngine = engine;
            uint activeVoiceId = voiceId;
            if (activeEngine == null || activeVoiceId == 0)
            {
                return;
            }

            if (hasPendingPacket && !TrySubmitPendingPacket(activeEngine, activeVoiceId))
            {
                return;
            }

            for (int packetIndex = 0; packetIndex < MaximumPacketsPerUpdate; packetIndex++)
            {
                MvcInterop.Result result = activeEngine.PullPacket(packetBytes, out pendingPacket);
                if (result == MvcInterop.Result.NoData)
                {
                    return;
                }

                if (result != MvcInterop.Result.Ok)
                {
                    ReportMainThreadFailure("pull packet", result);
                    return;
                }

                hasPendingPacket = true;
                if (!TrySubmitPendingPacket(activeEngine, activeVoiceId))
                {
                    return;
                }
            }
        }

        private bool TrySubmitPendingPacket(MvcInterop.Engine activeEngine, uint activeVoiceId)
        {
            MvcInterop.Result result = activeEngine.SubmitPacket(
                activeVoiceId,
                packetBytes,
                pendingPacket.Size,
                pendingPacket.Sequence,
                pendingPacket.Timestamp);

            if (result == MvcInterop.Result.WouldBlock)
            {
                return false;
            }

            hasPendingPacket = false;
            if (result != MvcInterop.Result.Ok)
            {
                ReportMainThreadFailure("submit packet", result);
            }

            return true;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null)
            {
                return;
            }

            if (!TryEnterAudioCallback())
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            try
            {
                MvcInterop.Engine activeEngine = engine;
                uint activeVoiceId = voiceId;
                if (activeEngine == null || activeVoiceId == 0 || channels != outputChannels ||
                    channels <= 0 || data.Length % channels != 0)
                {
                    Array.Clear(data, 0, data.Length);
                    return;
                }

                uint frameCount = (uint)(data.Length / channels);
                MvcInterop.Result pullResult = activeEngine.PullVoice(activeVoiceId, data, frameCount);
                if ((int)pullResult < 0)
                {
                    Interlocked.CompareExchange(ref audioThreadFailure, (int)pullResult, 0);
                    Array.Clear(data, 0, data.Length);
                }

            }
            finally
            {
                ExitAudioCallback();
            }
        }

        private void OnAudioListenerRender(AudioListenerVcInput.OnAudioFilterReadFrame frame)
        {
            if (Volatile.Read(ref renderReferenceEnabled) == 0 ||
                frame.data == null ||
                frame.dataLength <= 0 ||
                frame.dataLength > frame.data.Length ||
                frame.channels <= 0 ||
                frame.dataLength % frame.channels != 0)
            {
                return;
            }

            if (!TryEnterAudioCallback())
            {
                return;
            }

            try
            {
                MvcInterop.Engine activeEngine = engine;
                if (activeEngine == null ||
                    frame.sampleRateHz != outputSampleRate ||
                    frame.channels != outputChannels)
                {
                    return;
                }

                uint frameCount = (uint)(frame.dataLength / frame.channels);
                MvcInterop.Result result = activeEngine.SubmitRender(
                    frame.data,
                    frameCount,
                    Volatile.Read(ref cachedRenderDelayMs));

                if ((int)result < 0)
                {
                    Interlocked.CompareExchange(ref audioThreadFailure, (int)result, 0);
                }
            }
            finally
            {
                ExitAudioCallback();
            }
        }

        private bool TryEnterAudioCallback()
        {
            if (Volatile.Read(ref callbacksBlocked) != 0)
            {
                return false;
            }

            Interlocked.Increment(ref audioCallbackDepth);
            if (Volatile.Read(ref callbacksBlocked) == 0)
            {
                return true;
            }

            Interlocked.Decrement(ref audioCallbackDepth);
            return false;
        }

        private void ExitAudioCallback()
        {
            Interlocked.Decrement(ref audioCallbackDepth);
        }

        private void CreatePlaybackClip(int sampleRate)
        {
            if (playbackClip != null)
            {
                Destroy(playbackClip);
            }

            playbackClip = AudioClip.Create(
                nameof(OnAudioFilterReadVcOutput),
                sampleRate,
                1,
                sampleRate,
                false);

            playbackClipSamples = new float[sampleRate];
            for (int sample = 0; sample < playbackClipSamples.Length; sample++)
            {
                playbackClipSamples[sample] = 1f;
            }

            playbackClip.SetData(playbackClipSamples, 0);
            audioSource.clip = playbackClip;
        }

        private void ReportMainThreadFailure(string operation, MvcInterop.Result result)
        {
            if (failureLogged)
            {
                return;
            }

            failureLogged = true;
            Debug.LogError($"Native {operation} failed with {result}.", this);
        }

        private uint GetPacketDurationMs()
        {
            return audioFilterReadConfig != null
                ? (uint)audioFilterReadConfig.packetDuration
                : DefaultPacketDurationMs;
        }

        private int GetConfiguredRenderDelayMs()
        {
            return audioFilterReadConfig != null
                ? audioFilterReadConfig.renderDelayMs
                : DefaultRenderDelayMs;
        }

        private MvcInterop.ProcessingFlags GetProcessingFlags()
        {
            return audioFilterReadConfig != null
                ? audioFilterReadConfig.ProcessingFlags
                : MvcInterop.ProcessingFlags.Default;
        }

        private static void ConfigureAudioSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = true;
            source.pitch = 1f;
            source.dopplerLevel = 0f;
            source.bypassReverbZones = true;
            source.reverbZoneMix = 0f;
            source.spatializePostEffects = false;
        }

        private static int GetSpeakerModeChannelCount(AudioSpeakerMode speakerMode)
        {
            switch (speakerMode)
            {
                case AudioSpeakerMode.Mono:
                    return 1;
                case AudioSpeakerMode.Stereo:
                case AudioSpeakerMode.Prologic:
                    return 2;
                case AudioSpeakerMode.Quad:
                    return 4;
                case AudioSpeakerMode.Surround:
                    return 5;
                case AudioSpeakerMode.Mode5point1:
                    return 6;
                case AudioSpeakerMode.Mode7point1:
                    return 8;
                default:
                    return 0;
            }
        }

        private static bool IsSupportedPlatform()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            return IntPtr.Size == 8;
#else
            return false;
#endif
        }
    }
}
