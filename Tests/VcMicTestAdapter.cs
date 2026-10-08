#if UNITY_EDITOR && META_VOICE_CHAT_TESTS
namespace MetaVoiceChat.Input.Mic
{
    public partial class VcMic
    {
        public static class EditorTestAdapter
        {
            public static int Resync(int readAbsPos, int currAbsPos, int ringSamples, int marginSamples) =>
                VcMic.Resync(readAbsPos, currAbsPos, ringSamples, marginSamples);

            public static double Refill(double budget, double elapsedSeconds, int ringSamples, int samplesPerSecond) =>
                VcMic.Refill(budget, elapsedSeconds, ringSamples, samplesPerSecond);
        }
    }
}
#endif
