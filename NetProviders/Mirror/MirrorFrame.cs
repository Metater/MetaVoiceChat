#if MIRROR
using System;
using System.Buffers;
using Mirror;

namespace Assets.Metater.MetaVoiceChat.NetProvider.Mirror
{
    public readonly struct MirrorFrame
    {
        public readonly int index;
        public readonly double timestamp;
        public readonly ArraySegment<byte> data;
        public readonly bool isArrayRented;

        public ushort Length => (ushort)data.Count;

        public MirrorFrame(int index, double timestamp, ArraySegment<byte> data)
        {
            this.index = index;
            this.timestamp = timestamp;
            this.data = data;
            isArrayRented = false;
        }

        public MirrorFrame(int index, double timestamp)
        {
            this.index = index;
            this.timestamp = timestamp;
            data = ArraySegment<byte>.Empty;
            isArrayRented = false;
        }

        public MirrorFrame(int index, double timestamp, ReadOnlySpan<byte> data)
        {
            this.index = index;
            this.timestamp = timestamp;

            var array = ArrayPool<byte>.Shared.Rent(data.Length);
            data.CopyTo(array);
            this.data = new ArraySegment<byte>(array, 0, data.Length);

            isArrayRented = true;
        }

        public void ReturnArray()
        {
            if (!isArrayRented)
            {
                throw new Exception("Attempted to return a MirrorFrame without a rented array.");
            }

            ArrayPool<byte>.Shared.Return(data.Array);
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
                writer.WriteBytes(value.data.Array, 0, value.Length);
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
