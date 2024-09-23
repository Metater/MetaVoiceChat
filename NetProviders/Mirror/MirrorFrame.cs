#if MIRROR
using System;
using Mirror;

namespace Assets.Metater.MetaVoiceChat.NetProviders.Mirror
{
    public readonly struct MirrorFrame
    {
        public readonly int index;
        public readonly double timestamp;
        public readonly ArraySegment<byte> data;

        public ushort Length => (ushort)data.Count;

        public MirrorFrame(int index, double timestamp, ArraySegment<byte> data)
        {
            this.index = index;
            this.timestamp = timestamp;
            this.data = data;
        }

        public MirrorFrame(int index, double timestamp)
        {
            this.index = index;
            this.timestamp = timestamp;
            data = ArraySegment<byte>.Empty;
        }
    }

    public static class MirrorFrameReaderWriter
    {
        public static void WriteMirrorFrame(this NetworkWriter writer, MirrorFrame value)
        {
            writer.WriteInt(value.index);
            writer.WriteDouble(value.timestamp);
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
            ushort length = reader.ReadUShort();
            if (length != 0)
            {
                var data = reader.ReadBytesSegment(length);
                return new MirrorFrame(index, timestamp, data);
            }
            else
            {
                return new MirrorFrame(index, timestamp);
            }
        }
    }
}
#endif
