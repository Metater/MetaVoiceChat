#if MIRROR
using Assets.Metater.MetaVoiceChat.General;
using Mirror;

namespace Assets.Metater.MetaVoiceChat.VcImpls.Mirror
{
    public static class VcFrameReaderWriter
    {
        public static void WriteVcFrame(this NetworkWriter writer, VcFrame value)
        {
            writer.WriteBool(value.IsSpeaking);
            writer.WriteDouble(value.Timestamp);

            if (value.IsSpeaking)
            {
                var length = value.GetLength();
                writer.WriteUShort(length);
                writer.WriteBytes(value.Data.Array, 0, length);
            }
        }

        public static VcFrame ReadVcFrame(this NetworkReader reader)
        {
            bool isSpeaking = reader.ReadBool();
            double timestamp = reader.ReadDouble();

            if (isSpeaking)
            {
                ushort length = reader.ReadUShort();
                var data = reader.ReadBytesSegment(length);
                return new VcFrame(timestamp, data);
            }
            else
            {
                return new VcFrame(timestamp, VcFrameType.EncodedData);
            }
        }
    }
}
#endif
