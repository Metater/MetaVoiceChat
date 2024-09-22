#if MIRROR
using System;
using Mirror;

namespace Assets.Metater.MetaVoiceChat.NetProvider.Mirror
{
    public readonly struct MirrorFrame
    {
        public readonly double timestamp;
        public readonly ArraySegment<byte> data;

        public ushort Length => (ushort)data.Count;

        public MirrorFrame(double timestamp, ArraySegment<byte> data)
        {
            this.timestamp = timestamp;
            this.data = data;
        }

        public MirrorFrame(double timestamp)
        {
            this.timestamp = timestamp;
            data = ArraySegment<byte>.Empty;
        }
    }

    public static class MirrorFrameReaderWriter
    {
        public static void WriteMirrorFrame(this NetworkWriter writer, MirrorFrame value)
        {
            writer.WriteDouble(value.timestamp);
            writer.WriteUShort(value.Length);
            if (value.Length != 0)
            {
                writer.WriteBytes(value.data.Array, 0, value.Length);
            }
        }

        public static MirrorFrame ReadMirrorFrame(this NetworkReader reader)
        {
            double timestamp = reader.ReadDouble();
            ushort length = reader.ReadUShort();
            if (length != 0)
            {
                var data = reader.ReadBytesSegment(length);
                return new MirrorFrame(timestamp, data);
            }
            else
            {
                return new MirrorFrame(timestamp);
            }
        }
    }
}
#endif