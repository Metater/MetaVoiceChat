using System;

namespace MetaVoiceChat.Core.AEC3
{
    public class AutomaticGainControl2VcProcessor : IVcProcessor, IDisposable
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Process(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {
            throw new NotImplementedException();
        }
    }
}
