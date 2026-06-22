namespace MetaVoiceChat.Core
{
    public static class MetaVoiceChatConstants
    {
        // 48 kHz, 2 channels, 40 ms frame size
        public const int MaxPossibleFrameSizeInSamples = 48000 * 2 * 40 / 1000;

        public const int MaxPacketSize = 1275; // Maximum packet size for Opus
    }
}
