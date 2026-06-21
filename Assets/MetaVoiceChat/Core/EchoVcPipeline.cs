using System;

namespace MetaVoiceChat.Core
{
    public class EchoVcPipeline : VcPipeline
    {
        public OnAudioFilterReadVcOutput[] outputs;

        public override void Process(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {
            if (outputs != null)
            {
                foreach (var output in outputs)
                {
                    if (output != null)
                    {
                        output.Process(frame, frameSize, frequency, channels, sequenceNumber, timestamp);
                    }
                }
            }
        }
    }
}
