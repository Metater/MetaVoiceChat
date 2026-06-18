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
        [Min(0f)]
        public float reconnectInitialDelay = MicVcInput.DefaultReconnectInitialDelay;

        [Tooltip("Seconds between checks while no microphone devices are available.")]
        [Min(MicVcInput.MinimumReconnectPollInterval)]
        public float reconnectPollInterval = MicVcInput.DefaultReconnectPollInterval;

        [Tooltip("Seconds to wait after Unity reports a microphone start failure.")]
        [Min(MicVcInput.MinimumReconnectFailureTimeout)]
        public float reconnectFailureTimeout = MicVcInput.DefaultReconnectFailureTimeout;
    }
}
