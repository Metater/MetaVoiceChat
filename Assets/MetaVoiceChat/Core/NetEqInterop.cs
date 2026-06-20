using System;
using System.Runtime.InteropServices;

namespace MetaVoiceChat.Core
{
    /// <summary>
    /// Managed wrapper around the native MetaVoiceChat NetEQ library.
    ///
    /// Native library loading is handled by Unity:
    ///
    /// - Unity's Plugin Importer selects the correct binary for the current platform
    ///   and CPU architecture.
    /// - Each native plugin binary should have "Load on startup" enabled.
    /// - The C# side references only the logical library name.
    ///
    /// Expected native binary names:
    ///
    /// Windows:
    ///     meta_voice_chat_neteq.dll
    ///
    /// Android / Linux:
    ///     libmeta_voice_chat_neteq.so
    ///
    /// macOS:
    ///     libmeta_voice_chat_neteq.dylib
    ///
    /// iOS player builds:
    ///     linked into the app binary and resolved through "__Internal"
    /// </summary>
    public static class NetEqInterop
    {
#if UNITY_IOS && !UNITY_EDITOR
        /// <summary>
        /// iOS native plugin symbols are resolved from the final linked app binary.
        /// </summary>
        private const string LibraryName = "__Internal";
#else
        /// <summary>
        /// Logical native library name.
        ///
        /// Do not include "lib" prefixes or file extensions here.
        ///
        /// Unity / the platform loader maps this to:
        ///
        /// - meta_voice_chat_neteq.dll on Windows
        /// - libmeta_voice_chat_neteq.so on Android/Linux
        /// - libmeta_voice_chat_neteq.dylib on macOS
        ///
        /// Architecture selection should be configured through Unity's Plugin Importer.
        /// </summary>
        private const string LibraryName = "meta_voice_chat_neteq";
#endif

        /// <summary>
        /// Represents one native NetEQ instance.
        ///
        /// SafeHandle owns the native pointer lifetime.
        ///
        /// Important behavior:
        /// - Dispose releases the native NetEQ instance deterministically.
        /// - If Dispose is forgotten, SafeHandle provides a finalizer-backed fallback.
        /// - Passing SafeHandle directly to P/Invoke keeps the native handle alive
        ///   for the duration of the native call.
        ///
        /// Threading contract:
        /// - Do not call InsertPacket, GetAudio, or CurrentBufferSizeMs concurrently
        ///   on the same Instance.
        /// - It is okay for different Instance objects to be used from different
        ///   audio threads.
        /// - Native free_neteq must be safe to call from any thread as long as no
        ///   native call is concurrently using the same handle. SafeHandle protects
        ///   against release during an active P/Invoke that receives the SafeHandle.
        /// </summary>
        public sealed class Instance : IDisposable
        {
            private readonly NetEqSafeHandle handle;

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

                handle = create_neteq(
                    sampleRate,
                    channels,
                    maxPacketsInBuffer,
                    maxDelayMs,
                    minDelayMs,
                    additionalDelayMs);

                if (handle == null || handle.IsInvalid)
                {
                    handle?.Dispose();
                    throw new InvalidOperationException("Failed to create native NetEQ instance.");
                }
            }

            /// <summary>
            /// Current target / buffered delay reported by the native NetEQ instance,
            /// in milliseconds.
            /// </summary>
            public int CurrentBufferSizeMs
            {
                get
                {
                    return current_buffer_size_ms(GetLiveHandle());
                }
            }

            /// <summary>
            /// Inserts one decoded packet worth of PCM samples into the native NetEQ jitter buffer.
            ///
            /// samplesLen is the number of float samples to read from samples.
            /// For interleaved multi-channel audio, this is total sample count,
            /// not frame count.
            /// </summary>
            public void InsertPacket(
                ushort sequenceNumber,
                uint timestamp,
                float[] samples,
                int samplesLen,
                int sampleRate,
                int channels,
                int durationMs)
            {
                ValidateSamples(samples, samplesLen);
                ValidatePositive(sampleRate, nameof(sampleRate));
                ValidatePositive(channels, nameof(channels));
                ValidatePositive(durationMs, nameof(durationMs));

                insert_packet(
                    GetLiveHandle(),
                    sequenceNumber,
                    timestamp,
                    samples,
                    samplesLen,
                    sampleRate,
                    channels,
                    durationMs);
            }

            /// <summary>
            /// Reads PCM output from the native NetEQ instance.
            ///
            /// samplesLen is the maximum number of float samples that may be written
            /// into samples.
            ///
            /// Returns the number of samples written by the native side.
            /// </summary>
            public int GetAudio(float[] samples, int samplesLen)
            {
                ValidateSamples(samples, samplesLen);

                return get_audio(
                    GetLiveHandle(),
                    samples,
                    samplesLen);
            }

            /// <summary>
            /// Releases the native NetEQ instance.
            ///
            /// SafeHandle makes this idempotent.
            /// </summary>
            public void Dispose()
            {
                handle.Dispose();
            }

            private NetEqSafeHandle GetLiveHandle()
            {
                if (handle.IsClosed || handle.IsInvalid)
                {
                    throw new ObjectDisposedException(nameof(Instance));
                }

                return handle;
            }
        }

        /// <summary>
        /// Creates a managed wrapper around a native NetEQ instance.
        /// </summary>
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

        /// <summary>
        /// SafeHandle wrapper for the native NetEQ pointer.
        ///
        /// This is the right ownership primitive for native resources:
        /// - it avoids manual finalizer code on Instance,
        /// - it avoids double-free,
        /// - and P/Invoke knows how to keep it alive during native calls.
        /// </summary>
        private sealed class NetEqSafeHandle : SafeHandle
        {
            /// <summary>
            /// Required by the P/Invoke marshaller.
            /// </summary>
            private NetEqSafeHandle()
                : base(IntPtr.Zero, true)
            {
            }

            public override bool IsInvalid
            {
                get
                {
                    return handle == IntPtr.Zero;
                }
            }

            protected override bool ReleaseHandle()
            {
                free_neteq(handle);
                return true;
            }
        }

        [DllImport(LibraryName, EntryPoint = "create_neteq", CallingConvention = CallingConvention.Cdecl)]
        private static extern NetEqSafeHandle create_neteq(
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
            NetEqSafeHandle ptr,
            ushort sequenceNumber,
            uint timestamp,
            [In] float[] samples,
            int samplesLen,
            int sampleRate,
            int channels,
            int durationMs);

        [DllImport(LibraryName, EntryPoint = "get_audio", CallingConvention = CallingConvention.Cdecl)]
        private static extern int get_audio(
            NetEqSafeHandle ptr,
            [Out] float[] samples,
            int samplesLen);

        [DllImport(LibraryName, EntryPoint = "current_buffer_size_ms", CallingConvention = CallingConvention.Cdecl)]
        private static extern int current_buffer_size_ms(NetEqSafeHandle ptr);

        private static void ValidateSamples(float[] samples, int samplesLen)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            ValidatePositive(samplesLen, nameof(samplesLen));

            if (samplesLen > samples.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(samplesLen),
                    "Sample length cannot exceed the array length.");
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