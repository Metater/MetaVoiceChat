using System;

namespace MetaVoiceChat.Core
{
    public class MulticastVcPipeline : VcPipeline
    {
        public VcPipeline[] pipelines;

        public override void Process(ReadOnlySpan<float> frame, int frameSize, int inputFrequency, int inputChannels, ushort sequenceNumber, uint timestamp)
        {
            if (pipelines != null)
            {
                foreach (var pipeline in pipelines)
                {
                    if (pipeline != null)
                    {
                        pipeline.Process(frame, frameSize, inputFrequency, inputChannels, sequenceNumber, timestamp);
                    }
                }
            }
        }
    }
}
