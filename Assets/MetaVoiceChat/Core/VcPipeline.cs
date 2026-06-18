using System;
using UnityEngine;

namespace MetaVoiceChat.Core
{
    public class VcPipeline : MonoBehaviour, IVcProcessor
    {
        public void Process(ReadOnlySpan<float> frame, int frameSize, int inputFrequency, int inputChannels, ushort sequenceNumber, uint timestamp)
        {

        }
    }
}
