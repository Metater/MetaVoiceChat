namespace MetaVoiceChat.Core
{
    // Opus supports 12 kHz, but NetEQ doesn't

    [System.Serializable]
    public enum VcFrequency : byte
    {
        Hz48000,
        Hz24000,
        Hz16000,
        //Hz12000,
        Hz8000,
    }

    public static class VcFrequencyExtensions
    {
        public static int ToInt(this VcFrequency frequency)
        {
            return frequency switch
            {
                VcFrequency.Hz48000 => 48000,
                VcFrequency.Hz24000 => 24000,
                VcFrequency.Hz16000 => 16000,
                //VcFrequency.Hz12000 => 12000,
                VcFrequency.Hz8000 => 8000,
                _ => 48000,
            };
        }

        public static bool IsValid(this VcFrequency frequency)
        {
            return frequency == VcFrequency.Hz48000 ||
                   frequency == VcFrequency.Hz24000 ||
                   frequency == VcFrequency.Hz16000 ||
                   //frequency == VcFrequency.Hz12000 ||
                   frequency == VcFrequency.Hz8000;
        }
    }
}