using System;

namespace MetaVoiceChat.Core.AEC3
{
    public class AcousticEchoCancellation3VcProcessor : IVcProcessor, IDisposable
    {
        public void Dispose()
        {

        }

        public void Process(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {

        }
    }
}
