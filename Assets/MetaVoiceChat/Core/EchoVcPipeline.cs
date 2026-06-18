using System;

namespace MetaVoiceChat.Core
{
    public class EchoVcPipeline : VcPipeline
    {
        public OnAudioFilterReadVcOutput[] outputs;

        public override void Process(ReadOnlySpan<float> frame, int frameSize, int inputFrequency, int inputChannels, ushort sequenceNumber, uint timestamp)
        {
            if (outputs != null)
            {
                foreach (var output in outputs)
                {
                    if (output != null)
                    {
                        output.Process(frame, frameSize, inputFrequency, inputChannels, sequenceNumber, timestamp);
                    }
                }
            }
        }
    }
}
