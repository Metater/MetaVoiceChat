using UnityEngine;

namespace MetaVoiceChat.Core
{
    [CreateAssetMenu(fileName = "New Mic VC Config", menuName = "MetaVoiceChat/MicVcConfig", order = 4)]
    public class MicVcConfig : ScriptableObject
    {
        [Header("Automatic Reconnection")]

        [Tooltip("When enabled, the microphone input retries failed starts and reconnects after device loss.")]
        public bool autoReconnect = true;

        [Tooltip("Seconds to wait before the first retry after startup when no microphone can be opened.")]
        [Range(0f, 10f)]
        public float reconnectInitialDelay = MicVcInput.DefaultReconnectInitialDelay;

        [Tooltip("Seconds between retry attempts while no microphone can be opened.")]
        [Range(MicVcInput.MinimumReconnectPollInterval, 10f)]
        public float reconnectPollInterval = MicVcInput.DefaultReconnectPollInterval;

        [Tooltip("Seconds to wait after Unity reports a microphone start failure.")]
        [Range(MicVcInput.MinimumReconnectFailureTimeout, 10f)]
        public float reconnectFailureTimeout = MicVcInput.DefaultReconnectFailureTimeout;

        [Header("Device Discovery")]

        [Tooltip("Seconds between Unity microphone device list refreshes.")]
        [Range(MicVcInput.MinimumDeviceRefreshInterval, 10f)]
        public float deviceRefreshInterval = MicVcInput.DefaultDeviceRefreshInterval;
    }
}
