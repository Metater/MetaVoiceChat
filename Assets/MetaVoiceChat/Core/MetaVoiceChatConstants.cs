namespace MetaVoiceChat.Core
{
    public static class MetaVoiceChatConstants
    {
        // 48 kHz, 2 channels, 40 ms frame size
        public const int MaxPossibleFrameSizeInSamples = 48000 * 2 * 40 / 1000;

        // 48 kHz, 1 channel, 40 ms frame size
        public const int MaxPossibleFrameSizeInSamplesMono = 48000 * 1 * 40 / 1000;
    }
}
