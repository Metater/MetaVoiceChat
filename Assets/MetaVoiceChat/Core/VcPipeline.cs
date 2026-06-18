using System;
using UnityEngine;

namespace MetaVoiceChat.Core
{
    public class VcPipeline : MonoBehaviour, IVcProcessor
    {
        public OnAudioFilterReadVcOutput output;

        public void Process(ReadOnlySpan<float> frame, int frameSize, int inputFrequency, int inputChannels, ushort sequenceNumber, uint timestamp)
        {
            output.Process(frame, frameSize, inputFrequency, inputChannels, sequenceNumber, timestamp);
        }
    }
}
