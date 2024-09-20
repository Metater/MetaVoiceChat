using System;
using System.Collections.Generic;
using Mirror;

// https://mirror-networking.gitbook.io/docs/manual/guides/serialization#adding-custom-read-write-functions

namespace Assets.Metater.MetaVoiceChat
{
    public static class VcSegmentReaderWriter
    {
        public static void WriteVcSegment(this NetworkWriter writer, VcSegment segment)
        {
            writer.WriteUInt(segment.netId);
            writer.WriteInt(segment.segmentIndex);

            int segmentLength = VcSegment.Registry.SegmentLength;
            writer.WriteInt(segmentLength);
            short[] compressed = segment.CompressedBuffer;
            for (int i = 0; i < segmentLength; i++)
            {
                writer.WriteShort(compressed[i]);
            }
        }

        public static VcSegment ReadVcSegment(this NetworkReader reader)
        {
            uint netId = reader.ReadUInt();
            int segmentIndex = reader.ReadInt();

            int segmentLength = reader.ReadInt();
            VcSegment.Registry.ThrowIfSegmentLengthMismatch(segmentLength);
            float[] uncompressed = VcSegment.Registry.GetUncompressedBuffer(netId);
            for (int i = 0; i < segmentLength; i++)
            {
                uncompressed[i] = (float)reader.ReadShort() / short.MaxValue;
            }

            return new(netId, segmentIndex);
        }
    }

    public readonly struct VcSegment
    {
        public readonly uint netId;
        public readonly int segmentIndex;

        public readonly float[] UncompressedBuffer => Registry.GetUncompressedBuffer(netId);
        public readonly short[] CompressedBuffer => Registry.GetCompressedBuffer(netId);

        public VcSegment(uint netId, int segmentIndex, float[] segment)
        {
            this.netId = netId;
            this.segmentIndex = segmentIndex;

            short[] compressed = CompressedBuffer;
            for (int i = 0; i < Registry.SegmentLength; i++)
            {
                // float value = segment[i] * short.MaxValue;
                // if (value > short.MaxValue || value < short.MinValue)
                // {
                //     UnityEngine.Debug.LogError(value);
                // }

                compressed[i] = (short)(segment[i] * short.MaxValue);
            }
        }

        public VcSegment(uint netId, int segmentIndex)
        {
            this.netId = netId;
            this.segmentIndex = segmentIndex;
        }

        public static class Registry
        {
            #region Buffers
            private static readonly Dictionary<uint, float[]> uncompressedBuffers = new();
            private static readonly Dictionary<uint, short[]> compressedBuffers = new();

            public static float[] GetUncompressedBuffer(uint netId)
            {
                if (!uncompressedBuffers.TryGetValue(netId, out var buffer))
                {
                    buffer = new float[SegmentLength];
                    uncompressedBuffers[netId] = buffer;
                }

                ThrowIfSegmentLengthMismatch(buffer.Length);
                return buffer;
            }

            public static short[] GetCompressedBuffer(uint netId)
            {
                if (!compressedBuffers.TryGetValue(netId, out var buffer))
                {
                    buffer = new short[SegmentLength];
                    compressedBuffers[netId] = buffer;
                }

                ThrowIfSegmentLengthMismatch(buffer.Length);
                return buffer;
            }
            #endregion

            #region Segment Length
            public static int SegmentLength { get; private set; } = -1;

            public static void SetSamplesPerSegment(int segmentLength)
            {
                if (SegmentLength == -1)
                {
                    SegmentLength = segmentLength;
                }
                else
                {
                    ThrowIfSegmentLengthMismatch(segmentLength);
                }
            }

            public static void ThrowIfSegmentLengthMismatch(int segmentLength)
            {
                if (segmentLength != SegmentLength)
                {
                    throw new Exception("Voice chat segment registry segment length mismatch!");
                }
            }
            #endregion
        }
    }
}