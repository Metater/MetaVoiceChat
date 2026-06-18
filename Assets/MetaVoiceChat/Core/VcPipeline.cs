using System;
using UnityEngine;

namespace MetaVoiceChat.Core
{
    public abstract class VcPipeline : MonoBehaviour, IVcProcessor
    {
        public abstract void Process(ReadOnlySpan<float> frame, int frameSize, int inputFrequency, int inputChannels, ushort sequenceNumber, uint timestamp);
    }
}
