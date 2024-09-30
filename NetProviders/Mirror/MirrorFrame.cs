#if MIRROR
using System;
using Mirror;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.NetProviders.Mirror
{
    public readonly struct MirrorFrame
    {
        public readonly int index;
        public readonly double timestamp;
        public readonly float additionalLatency;
        public readonly ArraySegment<byte> data;

        public ushort Length => (ushort)data.Count;

        public MirrorFrame(int index, double timestamp, float additionalLatency, ArraySegment<byte> data)
        {
            this.index = index;
            this.timestamp = timestamp;
            this.additionalLatency = additionalLatency;
            this.data = data;
        }

        public MirrorFrame(int index, double timestamp, float additionalLatency)
        {
            this.index = index;
            this.timestamp = timestamp;
            this.additionalLatency = additionalLatency;
            data = ArraySegment<byte>.Empty;
        }
    }

    public static class MirrorFrameReaderWriter
    {
        // There is probably a better place to set this limitation
        private const float MaxAdditionalLatency = 0.2f;

        public static void WriteMirrorFrame(this NetworkWriter writer, MirrorFrame value)
        {
            writer.WriteInt(value.index);
            writer.WriteDouble(value.timestamp);
            {
                float additionalLatency = value.additionalLatency;
                additionalLatency = Mathf.Clamp(additionalLatency, 0, MaxAdditionalLatency);
                float t = additionalLatency / MaxAdditionalLatency;
                writer.WriteByte((byte)(t * byte.MaxValue));
            }
            writer.WriteUShort(value.Length);
            if (value.Length != 0)
            {
                writer.WriteBytes(value.data.Array, value.data.Offset, value.Length);
            }
        }

        public static MirrorFrame ReadMirrorFrame(this NetworkReader reader)
        {
            int index = reader.ReadInt();
            double timestamp = reader.ReadDouble();
            float additionalLatency;
            {
                float t = (float)reader.ReadByte() / byte.MaxValue;
                additionalLatency = t * MaxAdditionalLatency;
            }
            ushort length = reader.ReadUShort();
            if (length != 0)
            {
                var data = reader.ReadBytesSegment(length);
                return new MirrorFrame(index, timestamp, additionalLatency, data);
            }
            else
            {
                return new MirrorFrame(index, timestamp, additionalLatency);
            }
        }
    }
}
#endif
