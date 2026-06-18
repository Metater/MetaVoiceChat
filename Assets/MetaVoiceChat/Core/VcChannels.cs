namespace MetaVoiceChat.Core
{
    [System.Serializable]
    public enum VcChannels : byte
    {
        Mono,
        Stereo,
    }

    public static class VcChannelsExtensions
    {
        public static int ToInt(this VcChannels channels)
        {
            return channels switch
            {
                VcChannels.Mono => 1,
                VcChannels.Stereo => 2,
                _ => throw new System.ArgumentOutOfRangeException(nameof(channels), "Invalid channels value"),
            };
        }

        public static VcChannels FromInt(int channels)
        {
            return channels switch
            {
                1 => VcChannels.Mono,
                2 => VcChannels.Stereo,
                _ => throw new System.ArgumentOutOfRangeException(nameof(channels), "Invalid channels value"),
            };
        }

        public static bool IsValid(this VcChannels channels)
        {
            return channels == VcChannels.Mono ||
                   channels == VcChannels.Stereo;
        }
    }
}