#if FUSION_2_0_OR_NEWER
using System;
using UnityEngine;

namespace MetaVoiceChat.NetProviders.Fusion
{
    public readonly struct FusionFrame
    {
        public readonly int index;
        public readonly double timestamp;
        public readonly float additionalLatency;
        public readonly ReadOnlyMemory<byte> data;

        public ushort Length => (ushort)data.Length;

        public FusionFrame(int index, double timestamp, float additionalLatency, ReadOnlyMemory<byte> data)
        {
            this.index = index;
            this.timestamp = timestamp;
            this.additionalLatency = additionalLatency;
            this.data = data;
        }

        private const float MaxAdditionalLatency = 0.2f;
        private const int HeaderSize = sizeof(int) + sizeof(double) + sizeof(byte) + sizeof(ushort);

        public static byte[] Serialize(int index, double timestamp, float additionalLatency, ReadOnlySpan<byte> data)
        {
            ushort length = (ushort)data.Length;
            byte[] result = new byte[HeaderSize + length];
            Span<byte> span = result;
    
            BitConverter.TryWriteBytes(span.Slice(0, 4), index);
            BitConverter.TryWriteBytes(span.Slice(4, 8), timestamp);
            span[12] = EncodeLatency(additionalLatency);
            BitConverter.TryWriteBytes(span.Slice(13, 2), length);
    
            if (length > 0)
                data.CopyTo(span.Slice(HeaderSize));
    
            return result;
        }

        public static byte[] Serialize(FusionFrame frame) => Serialize(frame.index, frame.timestamp, frame.additionalLatency, frame.data.Span);

        public static FusionFrame Deserialize(byte[] packed)
        {
            ReadOnlySpan<byte> span = packed;
    
            int index = BitConverter.ToInt32(span.Slice(0, 4));
            double timestamp = BitConverter.ToDouble(span.Slice(4, 8));
            float additionalLatency = DecodeLatency(span[12]);
            ushort length = BitConverter.ToUInt16(span.Slice(13, 2));
    
            ReadOnlyMemory<byte> data = length > 0
                ? new ReadOnlyMemory<byte>(packed, HeaderSize, length)
                : ReadOnlyMemory<byte>.Empty;
    
            return new FusionFrame(index, timestamp, additionalLatency, data);
        }

        private static byte EncodeLatency(float additionalLatency) => (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(additionalLatency, 0, MaxAdditionalLatency) * 255f / MaxAdditionalLatency), 0, 255);
        private static float DecodeLatency(byte encoded) => encoded * MaxAdditionalLatency / 255f;
    }
}
#endif