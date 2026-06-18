namespace MetaVoiceChat.Core
{
    [System.Serializable]
    public enum VcMilliseconds : byte
    {
        Ms20,
        Ms40,
        Ms10,
    }

    public static class VcMillisecondsExtensions
    {
        public static int ToInt(this VcMilliseconds milliseconds)
        {
            return milliseconds switch
            {
                VcMilliseconds.Ms10 => 10,
                VcMilliseconds.Ms20 => 20,
                VcMilliseconds.Ms40 => 40,
                _ => throw new System.ArgumentOutOfRangeException(nameof(milliseconds), "Invalid milliseconds value"),
            };
        }

        public static VcMilliseconds FromInt(int milliseconds)
        {
            return milliseconds switch
            {
                10 => VcMilliseconds.Ms10,
                20 => VcMilliseconds.Ms20,
                40 => VcMilliseconds.Ms40,
                _ => throw new System.ArgumentOutOfRangeException(nameof(milliseconds), "Invalid milliseconds value"),
            };
        }

        public static bool IsValid(this VcMilliseconds milliseconds)
        {
            return milliseconds == VcMilliseconds.Ms10 ||
                   milliseconds == VcMilliseconds.Ms20 ||
                   milliseconds == VcMilliseconds.Ms40;
        }
    }
}