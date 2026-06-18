//#define LOG_MicVcInput

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace MetaVoiceChat.Core
{
    public enum MicInputState
    {
        Stopped,
        WaitingForPermission,
        PermissionDenied,
        NoDevices,
        Starting,
        Recording,
        StartFailed,
        DeviceLost
    }

    [DisallowMultipleComponent]
    public sealed class MicVcInput : MonoBehaviour
    {
        public const int InputChannels = 1;
        public const int ClipLoopSeconds = 3;

        public const float DefaultReconnectInitialDelay = 0.5f;
        public const float DefaultReconnectPollInterval = 1f;
        public const float DefaultReconnectFailureTimeout = 2f;
        public const float DefaultDeviceRefreshInterval = 1f;
        public const float MinimumReconnectPollInterval = 0.25f;
        public const float MinimumReconnectFailureTimeout = 0.5f;
        public const float MinimumDeviceRefreshInterval = 0.25f;

        private const VcFrequency DefaultFrequency = VcFrequency.Hz48000;
        private const VcMilliseconds DefaultMilliseconds = VcMilliseconds.Ms20;
        private const float PositionStallStartupGraceSeconds = 1f;
        private const float PositionStallTimeoutSeconds = 2f;

        [Header("Voice Pipeline")]
        [Tooltip("Pipeline that receives mono microphone frames. The frame array is reused every call, so processors should copy data immediately if they need to keep it.")]
        [SerializeField] private VcPipeline vcPipeline;

        [Tooltip("Optional microphone input settings. Leave empty to use default reconnect behavior.")]
        [SerializeField] private MicVcConfig micVcConfig;

        [Header("Frame")]
        [Tooltip("Requested Unity microphone sample rate. Changing this at runtime restarts the microphone automatically.")]
        [SerializeField] private VcFrequency vcFrequency = DefaultFrequency;

        [Tooltip("Duration of each voice frame. Changing this at runtime changes the frame size without requiring an output rebuild.")]
        [SerializeField] private VcMilliseconds vcMilliseconds = DefaultMilliseconds;

        [Header("Device")]
        [Tooltip("Optional microphone device name. Leave empty to use the first available device. If auto reconnect is enabled and this device appears later, the input reconnects to it automatically.")]
        [SerializeField] private string selectedDevice = string.Empty;

        [Tooltip("Invoked when the active microphone device changes. The value is empty when no microphone is active.")]
        [SerializeField] private UnityEvent<string> onActiveDeviceChanged = new UnityEvent<string>();

        [Tooltip("Invoked when the microphone input state changes.")]
        [SerializeField] private UnityEvent<MicInputState> onStateChanged = new UnityEvent<MicInputState>();

#if LOG_MicVcInput
        [Header("Runtime Diagnostics")]
        [Tooltip("Shows live capture values in the inspector while playing.")]
        [SerializeField] private bool exposeRuntimeDiagnostics = true;

        [SerializeField, HideInInspector] private string runtimeActiveDevice = string.Empty;
        [SerializeField, HideInInspector] private int runtimeAvailableSamples;
        [SerializeField, HideInInspector] private int runtimeFramesSent;
        [SerializeField, HideInInspector] private int runtimeDroppedSamples;
#endif

        private AudioClip audioClip;
        private string activeDevice = string.Empty;
        private string[] devices = Array.Empty<string>();
        private bool hasDeviceSnapshot;
        private bool isRecording;
        private bool reconnectRequested;
        private bool positionInitialized;
        private MicInputState state = MicInputState.Stopped;
        private int requestedFrequency;
        private int actualFrequency;
        private int frameMilliseconds;
        private int frameSize;
        private int clipSamples;
        private int previousMicrophonePosition;
        private int lastObservedPosition;
        private long completedClipLoops;
        private long readAbsolutePosition;
        private float recordingStartTime;
        private float lastPositionAdvanceTime;
        private float nextReconnectAttemptTime;
        private float nextDeviceRefreshTime;
        private Coroutine permissionRequestCoroutine;
#if LOG_MicVcInput
        private const float WarningThrottleSeconds = 3f;
        private float nextNoDeviceWarningTime;
        private float nextStartFailureWarningTime;
        private float nextPipelineWarningTime;
        private float nextReadFailureWarningTime;
        private float nextOverrunWarningTime;
#endif
        private float[] readBuffer = Array.Empty<float>();
        private ushort sequenceNumber;
        private uint timestamp;

        public event Action<string> OnActiveDeviceChanged;
        public event Action<string[], string[]> OnDevicesChanged;
        public event Action<MicInputState> OnStateChanged;

        public VcPipeline Pipeline
        {
            get { return vcPipeline; }
            set { vcPipeline = value; }
        }

        public MicVcConfig Config
        {
            get { return micVcConfig; }
            set { micVcConfig = value; }
        }

        public VcFrequency VcFrequency
        {
            get { return vcFrequency; }
            set
            {
                VcFrequency sanitized = SanitizeFrequency(value);
                if (vcFrequency == sanitized)
                {
                    return;
                }

                vcFrequency = sanitized;
                if (isActiveAndEnabled)
                {
                    RequestReconnect();
                }
            }
        }

        public VcMilliseconds VcMilliseconds
        {
            get { return vcMilliseconds; }
            set
            {
                VcMilliseconds sanitized = SanitizeMilliseconds(value);
                if (vcMilliseconds == sanitized)
                {
                    return;
                }

                vcMilliseconds = sanitized;
                ApplyFrameSettings();
            }
        }

        public string SelectedDevice => selectedDevice;
        public string ActiveDevice => activeDevice;
        public MicInputState State => state;
        public bool IsRecording => isRecording;
        public AudioClip AudioClip => audioClip;
        public int RequestedFrequency => requestedFrequency;
        public int ActualFrequency => actualFrequency;
        public int FrameMilliseconds => frameMilliseconds;
        public int FrameSize => frameSize;
        public int ReadBufferSize => readBuffer.Length;
#if LOG_MicVcInput
        public int AvailableSamples => runtimeAvailableSamples;
        public int FramesSent => runtimeFramesSent;
        public int DroppedSamples => runtimeDroppedSamples;
        public bool ExposeRuntimeDiagnostics => exposeRuntimeDiagnostics;
#endif
        public bool AutoReconnect => GetAutoReconnect();
        public float ReconnectPollInterval => GetReconnectPollInterval();
        public float ReconnectFailureTimeout => GetReconnectFailureTimeout();
        public float DeviceRefreshInterval => GetDeviceRefreshInterval();

        public void SetSelectedDevice(string device)
        {
            string normalized = string.IsNullOrWhiteSpace(device) ? string.Empty : device;
            if (selectedDevice == normalized)
            {
                return;
            }

            selectedDevice = normalized;
            if (isActiveAndEnabled)
            {
                RequestReconnect();
            }
        }

        public bool ContainsDevice(string device)
        {
            if (string.IsNullOrEmpty(device))
            {
                return false;
            }

            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i] == device)
                {
                    return true;
                }
            }

            return false;
        }

        public void RefreshDevices()
        {
            string[] oldDevices = devices;
            string[] newDevices = Microphone.devices ?? Array.Empty<string>();

            bool changed = !hasDeviceSnapshot || !AreDeviceListsEqual(oldDevices, newDevices);
            devices = newDevices;
            hasDeviceSnapshot = true;

            if (changed)
            {
                OnDevicesChanged?.Invoke(oldDevices, newDevices);
            }

            if (Application.isPlaying)
            {
                nextDeviceRefreshTime = Time.realtimeSinceStartup + GetDeviceRefreshInterval();
            }
        }

        public bool StartRecording()
        {
            return StartRecordingInternal(scheduleReconnectOnFailure: true);
        }

        public void StopRecording()
        {
            StopRecordingInternal(resetActiveDevice: true);
        }

        private void Awake()
        {
            ValidateSerializedSettings();
            ApplyFrameSettings();
        }

        private void Reset()
        {
            if (vcPipeline == null)
            {
                TryGetComponent(out vcPipeline);
            }

            ValidateSerializedSettings();
            ApplyFrameSettings();
        }

        private void OnEnable()
        {
            ValidateSerializedSettings();
            RefreshDevices();
            ApplyFrameSettings();
            if (!Application.isPlaying)
            {
                return;
            }

            float reconnectInitialDelay = GetReconnectInitialDelay();
            nextReconnectAttemptTime = Time.realtimeSinceStartup + reconnectInitialDelay;
            reconnectRequested = false;

            if (!StartRecordingInternal(scheduleReconnectOnFailure: true))
            {
                if (GetAutoReconnect())
                {
                    nextReconnectAttemptTime = Math.Max(
                        nextReconnectAttemptTime,
                        Time.realtimeSinceStartup + reconnectInitialDelay);
                }
            }
        }

        private void OnDisable()
        {
            reconnectRequested = false;
            StopPermissionRequest();
            StopRecordingInternal(resetActiveDevice: true);
            ReleaseCaptureBuffers();
        }

        private void OnDestroy()
        {
            reconnectRequested = false;
            StopPermissionRequest();
            StopRecordingInternal(resetActiveDevice: true);
            ReleaseCaptureBuffers();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ValidateSerializedSettings();
            MaybeRefreshDevices();

            int oldRequestedFrequency = requestedFrequency;
            ApplyFrameSettings();

            if (isRecording && oldRequestedFrequency != requestedFrequency)
            {
                RequestReconnect();
            }

            bool autoReconnect = GetAutoReconnect();
            if (isRecording)
            {
                if (reconnectRequested || (autoReconnect && ShouldReconnect()))
                {
                    ReconnectNow();
                    return;
                }

                ReadAllAvailableMicrophoneData();
            }

            if (autoReconnect && !isRecording && Time.realtimeSinceStartup >= nextReconnectAttemptTime)
            {
                StartRecordingInternal(scheduleReconnectOnFailure: true);
            }
        }

        private void OnValidate()
        {
            ValidateSerializedSettings();
            if (!Application.isPlaying)
            {
                ApplyFrameSettings();
            }
        }

        private bool StartRecordingInternal(bool scheduleReconnectOnFailure)
        {
            if (!Application.isPlaying)
            {
                return false;
            }

            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                BeginMicrophonePermissionRequest(scheduleReconnectOnFailure);
                return false;
            }

            reconnectRequested = false;
            StopRecordingInternal(resetActiveDevice: false);

            ValidateSerializedSettings();
            RefreshDevices();
            ApplyFrameSettings();
            SetState(MicInputState.Starting);

            if (devices.Length == 0)
            {
                SetState(MicInputState.NoDevices);
                LogNoDeviceWarning();
                if (scheduleReconnectOnFailure && GetAutoReconnect())
                {
                    ScheduleReconnect(GetReconnectPollInterval());
                }

                SetActiveDevice(string.Empty);
                return false;
            }

            string device = ResolveDevice();
            if (string.IsNullOrEmpty(device))
            {
                SetState(MicInputState.NoDevices);
                LogNoDeviceWarning();
                if (scheduleReconnectOnFailure && GetAutoReconnect())
                {
                    ScheduleReconnect(GetReconnectPollInterval());
                }

                SetActiveDevice(string.Empty);
                return false;
            }

            AudioClip startedClip = Microphone.Start(device, true, ClipLoopSeconds, requestedFrequency);
            if (startedClip == null)
            {
                SetState(MicInputState.StartFailed);
                LogStartFailureWarning(device, "Unity returned a null AudioClip.");
                if (scheduleReconnectOnFailure && GetAutoReconnect())
                {
                    ScheduleReconnect(GetReconnectFailureTimeout());
                }

                SetActiveDevice(string.Empty);
                return false;
            }

            audioClip = startedClip;
            clipSamples = Math.Max(1, audioClip.samples);
            actualFrequency = audioClip.frequency > 0 ? audioClip.frequency : requestedFrequency;
            SetActiveDevice(device);

            if (audioClip.channels != InputChannels)
            {
                LogStartFailureWarning(
                    device,
                    $"Unity returned a microphone clip with {audioClip.channels} channels. {nameof(MicVcInput)} only sends mono frames.");
                StopRecordingInternal(resetActiveDevice: true);
                SetState(MicInputState.StartFailed);
                if (scheduleReconnectOnFailure && GetAutoReconnect())
                {
                    ScheduleReconnect(GetReconnectFailureTimeout());
                }

                return false;
            }

            if (actualFrequency != requestedFrequency)
            {
#if LOG_MicVcInput
                Debug.LogWarning(
                    $"{nameof(MicVcInput)} requested {requestedFrequency} Hz from \"{device}\", but Unity created a {actualFrequency} Hz microphone clip. Frames will be stamped with the actual clip frequency.",
                    this);
#endif
            }

            isRecording = true;
            SetState(MicInputState.Recording);
            ResetPositionStallTracking(previousMicrophonePosition);
            ResetReadStateToCurrentMicrophonePosition();
            ResetFrameClock();
            EnsureReadBuffer();
            nextReconnectAttemptTime = float.PositiveInfinity;
            return true;
        }

        private void StopRecordingInternal(bool resetActiveDevice)
        {
            string deviceToStop = activeDevice;

            isRecording = false;
            positionInitialized = false;
            clipSamples = 0;
            previousMicrophonePosition = 0;
            completedClipLoops = 0;
            readAbsolutePosition = 0;
            actualFrequency = requestedFrequency;

            if (!string.IsNullOrEmpty(deviceToStop) && Microphone.IsRecording(deviceToStop))
            {
                Microphone.End(deviceToStop);
            }

            if (audioClip != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(audioClip);
                }
                else
                {
                    DestroyImmediate(audioClip);
                }

                audioClip = null;
            }

            if (resetActiveDevice)
            {
                SetActiveDevice(string.Empty);
            }

            if (resetActiveDevice)
            {
                SetState(MicInputState.Stopped);
            }
        }

        private void ReconnectNow()
        {
            StopRecordingInternal(resetActiveDevice: true);
            reconnectRequested = false;
            StartRecordingInternal(scheduleReconnectOnFailure: true);
        }

        private void RequestReconnect()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            reconnectRequested = true;
            nextReconnectAttemptTime = Time.realtimeSinceStartup;
        }

        private void HandleAutomaticReconnectNeeded()
        {
            if (GetAutoReconnect())
            {
                SetState(MicInputState.DeviceLost);
                RequestReconnect();
                return;
            }

            StopRecordingInternal(resetActiveDevice: true);
        }

        private void ScheduleReconnect(float delaySeconds)
        {
            float delay = Math.Max(0f, delaySeconds);
            nextReconnectAttemptTime = Time.realtimeSinceStartup + delay;
        }

        private bool ShouldReconnect()
        {
            if (audioClip == null || string.IsNullOrEmpty(activeDevice))
            {
                SetState(MicInputState.DeviceLost);
                return true;
            }

            if (!ContainsDevice(activeDevice))
            {
                SetState(MicInputState.DeviceLost);
                return true;
            }

            if (!Microphone.IsRecording(activeDevice))
            {
                SetState(MicInputState.DeviceLost);
                return true;
            }

            if (!string.IsNullOrEmpty(selectedDevice) &&
                selectedDevice != activeDevice &&
                ContainsDevice(selectedDevice))
            {
                return true;
            }

            return false;
        }

        private void ReadAllAvailableMicrophoneData()
        {
            if (audioClip == null || string.IsNullOrEmpty(activeDevice) || frameSize <= 0)
            {
                return;
            }

            long currentAbsolutePosition = GetCurrentAbsoluteMicrophonePosition();
            if (currentAbsolutePosition < 0)
            {
                HandleAutomaticReconnectNeeded();
                return;
            }

            if (currentAbsolutePosition < readAbsolutePosition)
            {
                readAbsolutePosition = currentAbsolutePosition;
            }

            long availableSamples = currentAbsolutePosition - readAbsolutePosition;
#if LOG_MicVcInput
            runtimeAvailableSamples = availableSamples > int.MaxValue ? int.MaxValue : (int)Math.Max(0, availableSamples);
#endif
            if (availableSamples < frameSize)
            {
                return;
            }

            int readableSamples = (int)Math.Min(int.MaxValue, availableSamples / frameSize * frameSize);
            int maxReadableSamples = Math.Max(frameSize, clipSamples / frameSize * frameSize);
            if (readableSamples > maxReadableSamples)
            {
                int dropped = readableSamples - maxReadableSamples;
#if LOG_MicVcInput
                runtimeDroppedSamples += dropped;
#endif
                LogOverrunWarning(dropped, readableSamples);
                readableSamples = maxReadableSamples;
                readAbsolutePosition = currentAbsolutePosition - readableSamples;
            }

            if (readableSamples <= 0)
            {
                return;
            }

            EnsureReadBuffer();
            while (readableSamples >= frameSize)
            {
                int offsetSamples = PositiveModulo(readAbsolutePosition, clipSamples);
                if (!audioClip.GetData(readBuffer, offsetSamples))
                {
                    LogReadFailureWarning();
                    HandleAutomaticReconnectNeeded();
                    return;
                }

                DispatchFrames(readBuffer, frameSize);
                readAbsolutePosition += frameSize;
                readableSamples -= frameSize;
            }
        }

        private void DispatchFrames(float[] samples, int sampleCount)
        {
            VcPipeline pipeline = vcPipeline;
            if (pipeline == null || !pipeline.isActiveAndEnabled)
            {
                LogNoPipelineWarning();
                return;
            }

            if (sampleCount < frameSize)
            {
                return;
            }

            pipeline.Process(samples, frameSize, actualFrequency, InputChannels, sequenceNumber, timestamp);
            sequenceNumber = unchecked((ushort)(sequenceNumber + 1));
            timestamp = unchecked(timestamp + (uint)frameSize);
#if LOG_MicVcInput
            runtimeFramesSent++;
#endif
        }

        private long GetCurrentAbsoluteMicrophonePosition()
        {
            if (!Microphone.IsRecording(activeDevice))
            {
                return -1;
            }

            int position = Microphone.GetPosition(activeDevice);
            if (position < 0)
            {
                return -1;
            }

            position = Math.Min(position, Math.Max(0, clipSamples - 1));
            if (HasPositionStalled(position))
            {
                LogStartFailureWarning(activeDevice, "Unity microphone position stopped advancing.");
                return -1;
            }

            if (!positionInitialized)
            {
                previousMicrophonePosition = position;
                positionInitialized = true;
            }
            else if (position < previousMicrophonePosition)
            {
                completedClipLoops++;
            }

            previousMicrophonePosition = position;
            return completedClipLoops * (long)clipSamples + position;
        }

        private bool HasPositionStalled(int position)
        {
            float now = Time.realtimeSinceStartup;
            if (position != lastObservedPosition)
            {
                lastObservedPosition = position;
                lastPositionAdvanceTime = now;
                return false;
            }

            return now - recordingStartTime > PositionStallStartupGraceSeconds &&
                   now - lastPositionAdvanceTime > PositionStallTimeoutSeconds;
        }

        private void ResetReadStateToCurrentMicrophonePosition()
        {
            positionInitialized = false;
            completedClipLoops = 0;
            previousMicrophonePosition = 0;
            readAbsolutePosition = Math.Max(0, GetCurrentAbsoluteMicrophonePosition());
#if LOG_MicVcInput
            runtimeAvailableSamples = 0;
#endif
        }

        private void ResetPositionStallTracking(int position)
        {
            float now = Time.realtimeSinceStartup;
            recordingStartTime = now;
            lastObservedPosition = position;
            lastPositionAdvanceTime = now;
        }

        private void ResetFrameClock()
        {
            sequenceNumber = 0;
            timestamp = 0;
#if LOG_MicVcInput
            runtimeFramesSent = 0;
            runtimeDroppedSamples = 0;
#endif
        }

        private void ApplyFrameSettings()
        {
            requestedFrequency = SafeFrequencyToInt(vcFrequency);
            frameMilliseconds = SafeMillisecondsToInt(vcMilliseconds);
            actualFrequency = isRecording && audioClip != null && audioClip.frequency > 0
                ? audioClip.frequency
                : requestedFrequency;

            int nextFrameSize = Math.Max(1, actualFrequency * frameMilliseconds / 1000);
            if (frameSize != nextFrameSize)
            {
                frameSize = nextFrameSize;
                readBuffer = Array.Empty<float>();
            }

            EnsureReadBuffer();
        }

        private void EnsureReadBuffer()
        {
            if (frameSize <= 0 || readBuffer.Length == frameSize)
            {
                return;
            }

            readBuffer = new float[frameSize];
        }

        private void ReleaseCaptureBuffers()
        {
            readBuffer = Array.Empty<float>();
        }

        private void MaybeRefreshDevices()
        {
            if (Time.realtimeSinceStartup < nextDeviceRefreshTime)
            {
                return;
            }

            RefreshDevices();
        }

        private string ResolveDevice()
        {
            if (!string.IsNullOrEmpty(selectedDevice) && ContainsDevice(selectedDevice))
            {
                return selectedDevice;
            }

            return devices.Length > 0 ? devices[0] : string.Empty;
        }

        private void SetActiveDevice(string device)
        {
            string normalized = string.IsNullOrEmpty(device) ? string.Empty : device;
            if (activeDevice == normalized)
            {
#if LOG_MicVcInput
                runtimeActiveDevice = activeDevice;
#endif
                return;
            }

            activeDevice = normalized;
#if LOG_MicVcInput
            runtimeActiveDevice = activeDevice;
#endif
            onActiveDeviceChanged?.Invoke(activeDevice);
            OnActiveDeviceChanged?.Invoke(activeDevice);
        }

        private void SetState(MicInputState nextState)
        {
            if (state == nextState)
            {
                return;
            }

            state = nextState;
            onStateChanged?.Invoke(state);
            OnStateChanged?.Invoke(state);
        }

        private void BeginMicrophonePermissionRequest(bool scheduleReconnectOnFailure)
        {
            if (permissionRequestCoroutine != null)
            {
                return;
            }

            SetState(MicInputState.WaitingForPermission);
            nextReconnectAttemptTime = float.PositiveInfinity;
            permissionRequestCoroutine = StartCoroutine(RequestMicrophonePermission(scheduleReconnectOnFailure));
        }

        private IEnumerator RequestMicrophonePermission(bool scheduleReconnectOnFailure)
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            permissionRequestCoroutine = null;

            if (!isActiveAndEnabled)
            {
                yield break;
            }

            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                SetState(MicInputState.PermissionDenied);
                if (scheduleReconnectOnFailure && GetAutoReconnect())
                {
                    ScheduleReconnect(GetReconnectFailureTimeout());
                }

                yield break;
            }

            StartRecordingInternal(scheduleReconnectOnFailure);
        }

        private void StopPermissionRequest()
        {
            if (permissionRequestCoroutine == null)
            {
                return;
            }

            StopCoroutine(permissionRequestCoroutine);
            permissionRequestCoroutine = null;
        }

        private void ValidateSerializedSettings()
        {
            vcFrequency = SanitizeFrequency(vcFrequency);
            vcMilliseconds = SanitizeMilliseconds(vcMilliseconds);
            selectedDevice = string.IsNullOrWhiteSpace(selectedDevice) ? string.Empty : selectedDevice;
        }

        private bool GetAutoReconnect()
        {
            return micVcConfig == null || micVcConfig.autoReconnect;
        }

        private float GetReconnectInitialDelay()
        {
            return micVcConfig != null
                ? Math.Max(0f, micVcConfig.reconnectInitialDelay)
                : DefaultReconnectInitialDelay;
        }

        private float GetReconnectPollInterval()
        {
            return micVcConfig != null
                ? Math.Max(MinimumReconnectPollInterval, micVcConfig.reconnectPollInterval)
                : DefaultReconnectPollInterval;
        }

        private float GetReconnectFailureTimeout()
        {
            return micVcConfig != null
                ? Math.Max(MinimumReconnectFailureTimeout, micVcConfig.reconnectFailureTimeout)
                : DefaultReconnectFailureTimeout;
        }

        private float GetDeviceRefreshInterval()
        {
            return micVcConfig != null
                ? Math.Max(MinimumDeviceRefreshInterval, micVcConfig.deviceRefreshInterval)
                : DefaultDeviceRefreshInterval;
        }

        private void LogNoDeviceWarning()
        {
#if LOG_MicVcInput
            if (Time.realtimeSinceStartup < nextNoDeviceWarningTime)
            {
                return;
            }

            nextNoDeviceWarningTime = Time.realtimeSinceStartup + WarningThrottleSeconds;
            Debug.LogWarning(
                $"{nameof(MicVcInput)} could not find any microphone devices. " +
                (GetAutoReconnect() ? "It will keep reconnecting automatically." : "Auto reconnect is disabled."),
                this);
#endif
        }

        private void LogStartFailureWarning(string device, string reason)
        {
#if LOG_MicVcInput
            if (Time.realtimeSinceStartup < nextStartFailureWarningTime)
            {
                return;
            }

            nextStartFailureWarningTime = Time.realtimeSinceStartup + WarningThrottleSeconds;
            Debug.LogWarning(
                $"{nameof(MicVcInput)} failed to start microphone \"{device}\". {reason} " +
                (GetAutoReconnect() ? "It will retry automatically." : "Auto reconnect is disabled."),
                this);
#endif
        }

        private void LogNoPipelineWarning()
        {
#if LOG_MicVcInput
            if (Time.realtimeSinceStartup < nextPipelineWarningTime)
            {
                return;
            }

            nextPipelineWarningTime = Time.realtimeSinceStartup + WarningThrottleSeconds;
            Debug.LogWarning(
                $"{nameof(MicVcInput)} captured microphone frames but no {nameof(VcPipeline)} is assigned, so frames are being discarded.",
                this);
#endif
        }

        private void LogReadFailureWarning()
        {
#if LOG_MicVcInput
            if (Time.realtimeSinceStartup < nextReadFailureWarningTime)
            {
                return;
            }

            nextReadFailureWarningTime = Time.realtimeSinceStartup + WarningThrottleSeconds;
            Debug.LogWarning(
                $"{nameof(MicVcInput)} failed to read microphone clip data. " +
                (GetAutoReconnect() ? "It will reconnect automatically." : "Auto reconnect is disabled."),
                this);
#endif
        }

        private void LogOverrunWarning(int droppedSamples, int readableSamples)
        {
#if LOG_MicVcInput
            if (Time.realtimeSinceStartup < nextOverrunWarningTime)
            {
                return;
            }

            nextOverrunWarningTime = Time.realtimeSinceStartup + WarningThrottleSeconds;
            Debug.LogWarning(
                $"{nameof(MicVcInput)} fell behind the {ClipLoopSeconds} second microphone loop and dropped {droppedSamples} old samples before reading {readableSamples} samples.",
                this);
#endif
        }

        private static bool AreDeviceListsEqual(string[] left, string[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static VcFrequency SanitizeFrequency(VcFrequency frequency)
        {
            return frequency.IsValid() ? frequency : DefaultFrequency;
        }

        private static VcMilliseconds SanitizeMilliseconds(VcMilliseconds milliseconds)
        {
            return milliseconds.IsValid() ? milliseconds : DefaultMilliseconds;
        }

        private static int SafeFrequencyToInt(VcFrequency frequency)
        {
            return SanitizeFrequency(frequency).ToInt();
        }

        private static int SafeMillisecondsToInt(VcMilliseconds milliseconds)
        {
            return SanitizeMilliseconds(milliseconds).ToInt();
        }

        private static int PositiveModulo(long value, int divisor)
        {
            if (divisor <= 0)
            {
                return 0;
            }

            long result = value % divisor;
            if (result < 0)
            {
                result += divisor;
            }

            return (int)result;
        }
    }
}
