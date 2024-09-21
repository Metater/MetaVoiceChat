using System;
using System.Buffers;

namespace Assets.Metater.MetaVoiceChat.General
{
    public interface IVcFrame
    {
        bool IsSpeaking { get; }
        double Timestamp { get; }
        ArraySegment<byte> Data { get; }
        ArraySegment<short> Samples { get; }
        VcFrameType FrameType { get; }
        bool IsArrayRented { get; }
    }

    public enum VcFrameType
    {
        Null,
        EncodedData,
        DecodedSamples
    }

    public static class IVcFrameExtensions
    {
        public static ushort GetLength<T>(this T frame) where T : IVcFrame
        {
            return frame.FrameType switch
            {
                VcFrameType.Null => throw new Exception("Null VcFrameType."),
                VcFrameType.EncodedData => (ushort)frame.Data.Count,
                VcFrameType.DecodedSamples => (ushort)frame.Samples.Count,
                _ => throw new Exception("Invalid VcFrameType.")
            };
        }

        public static void Return<T>(this T frame) where T : IVcFrame
        {
            if (!frame.IsArrayRented)
            {
                throw new Exception("Attempted to return an IVcFrame without a rented array.");
            }

            switch (frame.FrameType)
            {
                case VcFrameType.Null:
                    throw new Exception("Null VcFrameType.");
                case VcFrameType.EncodedData:
                    ArrayPool<byte>.Shared.Return(frame.Data.Array);
                    break;
                case VcFrameType.DecodedSamples:
                    ArrayPool<short>.Shared.Return(frame.Samples.Array);
                    break;
                default:
                    throw new Exception("Invalid VcFrameType.");
            }
        }
    }
}