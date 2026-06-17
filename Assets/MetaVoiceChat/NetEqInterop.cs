using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace MetaVoiceChat
{
    public static class NetEqInterop
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string LibraryName = "__Internal";
#else
        private const string LibraryName = "meta_voice_chat_neteq";
#endif

        public sealed class Instance : IDisposable
        {
            private IntPtr ptr;
            public Instance(
                int sampleRate,
                int channels,
                int maxPacketsInBuffer,
                int maxDelayMs,
                int minDelayMs,
                int additionalDelayMs)
            {
                ValidatePositive(sampleRate, nameof(sampleRate));
                ValidatePositive(channels, nameof(channels));
                ValidatePositive(maxPacketsInBuffer, nameof(maxPacketsInBuffer));
                ValidateNonNegative(maxDelayMs, nameof(maxDelayMs));
                ValidateNonNegative(minDelayMs, nameof(minDelayMs));
                ValidateNonNegative(additionalDelayMs, nameof(additionalDelayMs));

                ptr = create_neteq(
                    sampleRate,
                    channels,
                    maxPacketsInBuffer,
                    maxDelayMs,
                    minDelayMs,
                    additionalDelayMs);

                if (ptr == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create native NetEq instance.");
                }
            }

            ~Instance()
            {
                Dispose(false);
            }

            public IntPtr NativePtr
            {
                get
                {
                    return GetLivePtr();
                }
            }

            public int CurrentBufferSizeMs
            {
                get
                {
                    return current_buffer_size_ms(GetLivePtr());
                }
            }

            public void InsertPacket(
                ushort sequenceNumber,
                uint timestamp,
                float[] samples,
                int samplesLen,
                int sampleRate,
                int channels,
                int durationMs)
            {
                IntPtr nativePtr = GetLivePtr();
                ValidateSamples(samples, samplesLen);
                ValidatePositive(sampleRate, nameof(sampleRate));
                ValidatePositive(channels, nameof(channels));
                ValidatePositive(durationMs, nameof(durationMs));

                insert_packet(
                    nativePtr,
                    sequenceNumber,
                    timestamp,
                    samples,
                    samplesLen,
                    sampleRate,
                    channels,
                    durationMs);
            }

            public int GetAudio(float[] samples, int samplesLen)
            {
                IntPtr nativePtr = GetLivePtr();
                ValidateSamples(samples, samplesLen);

                return get_audio(nativePtr, samples, samplesLen);
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing)
            {
                IntPtr nativePtr = Interlocked.Exchange(ref ptr, IntPtr.Zero);
                if (nativePtr != IntPtr.Zero)
                {
                    free_neteq(nativePtr);
                }
            }

            private IntPtr GetLivePtr()
            {
                IntPtr nativePtr = ptr;
                if (nativePtr == IntPtr.Zero)
                {
                    throw new ObjectDisposedException(nameof(Instance));
                }

                return nativePtr;
            }
        }

        public static Instance Create(
            int sampleRate,
            int channels,
            int maxPacketsInBuffer,
            int maxDelayMs,
            int minDelayMs,
            int additionalDelayMs)
        {
            return new Instance(
                sampleRate,
                channels,
                maxPacketsInBuffer,
                maxDelayMs,
                minDelayMs,
                additionalDelayMs);
        }

        [DllImport(LibraryName, EntryPoint = "create_neteq", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr create_neteq(
            int sampleRate,
            int channels,
            int maxPacketsInBuffer,
            int maxDelayMs,
            int minDelayMs,
            int additionalDelayMs);

        [DllImport(LibraryName, EntryPoint = "free_neteq", CallingConvention = CallingConvention.Cdecl)]
        private static extern void free_neteq(IntPtr ptr);

        [DllImport(LibraryName, EntryPoint = "insert_packet", CallingConvention = CallingConvention.Cdecl)]
        private static extern void insert_packet(
            IntPtr ptr,
            ushort sequenceNumber,
            uint timestamp,
            [In] float[] samples,
            int samplesLen,
            int sampleRate,
            int channels,
            int durationMs);

        [DllImport(LibraryName, EntryPoint = "get_audio", CallingConvention = CallingConvention.Cdecl)]
        private static extern int get_audio(
            IntPtr ptr,
            [Out] float[] samples,
            int samplesLen);

        [DllImport(LibraryName, EntryPoint = "current_buffer_size_ms", CallingConvention = CallingConvention.Cdecl)]
        private static extern int current_buffer_size_ms(IntPtr ptr);

        private static void ValidateSamples(float[] samples, int samplesLen)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            ValidatePositive(samplesLen, nameof(samplesLen));

            if (samplesLen > samples.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(samplesLen), "Sample length cannot exceed the array length.");
            }
        }

        private static void ValidatePositive(int value, string paramName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(paramName, "Value must be positive.");
            }
        }

        private static void ValidateNonNegative(int value, string paramName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, "Value cannot be negative.");
            }
        }
    }
}
