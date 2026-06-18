using System;

namespace MetaVoiceChat.Core
{
    public interface IVcOutput
    {
        /// <summary>
        /// Processes a frame of audio data for output.
        /// </summary>
        /// <param name="frame">PCM audio data</param>
        /// <param name="frameSize">PCM audio data length</param>
        /// <param name="inputFrequency">Input sampling rate, in hertz</param>
        /// <param name="inputChannels">Input channel count</param>
        /// <param name="sequenceNumber">Sequence number, incremented for each frame</param>
        /// <param name="timestamp">Timestamp, incremented by the number of samples per frame each frame (NOT MULTIPLIED BY CHANNELS)</param>
        void Process(
           ReadOnlySpan<float> frame,
           int frameSize,
           int inputFrequency,
           int inputChannels,
           ushort sequenceNumber,
           uint timestamp);
    }
}
