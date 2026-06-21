using System;

namespace MetaVoiceChat.Core
{
    public interface IVcDataProcessor
    {
        /// <summary>
        /// Processes a frame of encodedaudio data.
        /// </summary>
        /// <param name="data">Encoded audio data, there is no ownership transfer contract; the data should be copied immediately if needed</param>
        /// <param name="dataSize">Encoded audio data length</param>
        /// <param name="frequency">Sampling rate, in hertz</param>
        /// <param name="channels">Channel count</param>
        /// <param name="sequenceNumber">Sequence number, incremented for each frame</param>
        /// <param name="timestamp">Timestamp, incremented by the number of samples per frame each frame (NOT MULTIPLIED BY CHANNELS)</param>
        void Process(
           ReadOnlySpan<byte> data,
           int dataSize,
           int frequency,
           int channels,
           ushort sequenceNumber,
           uint timestamp);
    }
}
