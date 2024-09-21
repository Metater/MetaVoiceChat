using System;
using System.Buffers;

namespace Assets.Metater.MetaVoiceChat.General
{
    public readonly struct VcFrame : IVcFrame
    {
        public readonly bool IsSpeaking { get; }
        public readonly double Timestamp { get; }
        public readonly ArraySegment<byte> Data { get; }
        public readonly ArraySegment<short> Samples { get; }
        public readonly VcFrameType FrameType { get; }
        public readonly bool IsArrayRented { get; }

        public VcFrame(double timestamp, VcFrameType frameType)
        {
            IsSpeaking = false;
            Timestamp = timestamp;
            Data = ArraySegment<byte>.Empty;
            Samples = ArraySegment<short>.Empty;
            FrameType = frameType;
            IsArrayRented = false;
        }

        public VcFrame(double timestamp, ArraySegment<byte> data)
        {
            IsSpeaking = true;
            Timestamp = timestamp;
            Data = data;
            Samples = ArraySegment<short>.Empty;
            FrameType = VcFrameType.EncodedData;
            IsArrayRented = false;
        }

        public VcFrame(double timestamp, ArraySegment<short> samples)
        {
            IsSpeaking = true;
            Timestamp = timestamp;
            Data = ArraySegment<byte>.Empty;
            Samples = samples;
            FrameType = VcFrameType.DecodedSamples;
            IsArrayRented = false;
        }

        public VcFrame(double timestamp, ReadOnlySpan<byte> data)
        {
            var array = ArrayPool<byte>.Shared.Rent(data.Length);
            data.CopyTo(array);

            IsSpeaking = true;
            Timestamp = timestamp;
            Data = array;
            Samples = ArraySegment<short>.Empty;
            FrameType = VcFrameType.EncodedData;
            IsArrayRented = true;
        }

        public VcFrame(double timestamp, ReadOnlySpan<short> samples)
        {
            var array = ArrayPool<short>.Shared.Rent(samples.Length);
            samples.CopyTo(array);

            IsSpeaking = true;
            Timestamp = timestamp;
            Data = ArraySegment<byte>.Empty;
            Samples = array;
            FrameType = VcFrameType.DecodedSamples;
            IsArrayRented = true;
        }
    }
}