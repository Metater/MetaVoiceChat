using System;

namespace MetaVoiceChat.Core
{
    public class MulticastVcPipeline : VcPipeline
    {
        public VcPipeline[] pipelines;

        public override void Process(ReadOnlySpan<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
        {
            if (pipelines != null)
            {
                foreach (var pipeline in pipelines)
                {
                    if (pipeline != null)
                    {
                        pipeline.Process(frame, frameSize, frequency, channels, sequenceNumber, timestamp);
                    }
                }
            }
        }
    }
}
