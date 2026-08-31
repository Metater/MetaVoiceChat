using System;
using System.Runtime.InteropServices;

namespace MetaVoiceChat.Native
{
    /// <summary>
    /// Minimal managed binding for MetaVoiceChatNative ABI 3.
    /// </summary>
    public static class MvcInterop
    {
        private const string LibraryName = "MetaVoiceChatNative";

        /// <summary>The ABI implemented by this binding.</summary>
        public const uint ExpectedAbiVersion = 3;

        /// <summary>The maximum encoded Opus payload size accepted by the ABI.</summary>
        public const int MaxOpusPacketBytes = 1275;

        /// <summary>Native result codes.</summary>
        public enum Result : int
        {
            Ok = 0,
            NoData = 1,
            WouldBlock = 2,
            InvalidArgument = -1,
            InvalidState = -2,
            Error = -3,
        }

        /// <summary>Selects who supplies microphone PCM.</summary>
        public enum CaptureSource : uint
        {
            CallerPcm = 0,
            DefaultMicrophone = 1,
        }

        /// <summary>Optional native capture processing stages.</summary>
        [Flags]
        public enum ProcessingFlags : uint
        {
            None = 0,
            Aec3 = 1u << 0,
            HighPassFilter = 1u << 1,
            Rnnoise = 1u << 2,
            Agc2 = 1u << 3,
            Default = Aec3 | Rnnoise | Agc2,
        }

        /// <summary>Creation configuration copied by the native engine.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct Config
        {
            public CaptureSource CaptureSource;
            public uint CaptureInputChannels;
            public uint UnityOutputSampleRateHz;
            public uint UnityOutputChannels;
            public uint MaximumCallbackFrames;
            public uint OpusPacketDurationMs;
            public uint MaximumRemoteVoices;
            public ProcessingFlags ProcessingFlags;
        }

        /// <summary>Metadata returned with one encoded Opus payload.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct Packet
        {
            public uint Size;
            public uint Timestamp;
            public ushort Sequence;
        }

        /// <summary>
        /// Owns one native engine. Dispose deterministically stops and destroys it.
        /// </summary>
        public sealed class Engine : IDisposable
        {
            private readonly EngineSafeHandle handle;

            internal Engine(EngineSafeHandle handle)
            {
                this.handle = handle;
            }

            /// <summary>Starts the engine once.</summary>
            public Result Start()
            {
                return mvc_engine_start(GetLiveHandle());
            }

            /// <summary>Stops the engine. This operation is idempotent.</summary>
            public void Stop()
            {
                if (!handle.IsClosed && !handle.IsInvalid)
                {
                    mvc_engine_stop(handle);
                }
            }

            /// <summary>Submits caller-owned 48 kHz capture PCM.</summary>
            public Result SubmitCapture(float[] samples, uint frameCount)
            {
                return mvc_engine_submit_capture_f32(GetLiveHandle(), samples, frameCount);
            }

            /// <summary>Submits Unity's final or near-final speaker mix to AEC3.</summary>
            public Result SubmitRender(float[] samples, uint frameCount, int delayMs)
            {
                return mvc_engine_submit_render_f32(GetLiveHandle(), samples, frameCount, delayMs);
            }

            /// <summary>Pulls one encoded Opus packet.</summary>
            public Result PullPacket(byte[] bytes, out Packet packet)
            {
                if (bytes == null)
                {
                    throw new ArgumentNullException(nameof(bytes));
                }

                return mvc_engine_pull_packet(GetLiveHandle(), bytes, (uint)bytes.Length, out packet);
            }

            /// <summary>Adds one remote voice slot.</summary>
            public Result AddVoice(out uint voiceId)
            {
                return mvc_engine_add_voice(GetLiveHandle(), out voiceId);
            }

            /// <summary>Removes one remote voice slot.</summary>
            public void RemoveVoice(uint voiceId)
            {
                mvc_engine_remove_voice(GetLiveHandle(), voiceId);
            }

            /// <summary>Submits one complete raw Opus payload to a voice.</summary>
            public Result SubmitPacket(uint voiceId, byte[] bytes, uint size, ushort sequence, uint timestamp)
            {
                if (bytes == null)
                {
                    throw new ArgumentNullException(nameof(bytes));
                }

                if (size > (uint)bytes.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(size));
                }

                return mvc_engine_submit_packet(GetLiveHandle(), voiceId, bytes, size, sequence, timestamp);
            }

            /// <summary>
            /// Replaces an interleaved Unity spatialization mask with prepared voice audio.
            /// </summary>
            public Result PullVoice(uint voiceId, float[] spatializationMask, uint frameCount)
            {
                if (spatializationMask == null)
                {
                    throw new ArgumentNullException(nameof(spatializationMask));
                }

                return mvc_engine_pull_voice_f32(GetLiveHandle(), voiceId, spatializationMask, frameCount);
            }

            /// <summary>Releases the native engine.</summary>
            public void Dispose()
            {
                handle.Dispose();
            }

            private EngineSafeHandle GetLiveHandle()
            {
                if (handle.IsClosed || handle.IsInvalid)
                {
                    throw new ObjectDisposedException(nameof(Engine));
                }

                return handle;
            }
        }

        /// <summary>Gets the loaded native ABI version.</summary>
        public static uint GetAbiVersion()
        {
            return mvc_get_abi_version();
        }

        /// <summary>Gets the immutable native build description.</summary>
        public static string GetVersion()
        {
            return Marshal.PtrToStringAnsi(mvc_get_version()) ?? string.Empty;
        }

        /// <summary>Creates an engine and transfers its native ownership to a safe handle.</summary>
        public static Result Create(Config config, out Engine engine)
        {
            Result result = mvc_engine_create(ref config, out EngineSafeHandle handle);
            if (result != Result.Ok || handle == null || handle.IsInvalid)
            {
                handle?.Dispose();
                engine = null;
                return result == Result.Ok ? Result.Error : result;
            }

            engine = new Engine(handle);
            return Result.Ok;
        }

        internal sealed class EngineSafeHandle : SafeHandle
        {
            private EngineSafeHandle()
                : base(IntPtr.Zero, true)
            {
            }

            public override bool IsInvalid
            {
                get { return handle == IntPtr.Zero || handle == new IntPtr(-1); }
            }

            protected override bool ReleaseHandle()
            {
                try
                {
                    mvc_engine_destroy(handle);
                }
                catch
                {
                    // A SafeHandle finalizer must never throw.
                }

                handle = IntPtr.Zero;
                return true;
            }
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern uint mvc_get_abi_version();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern IntPtr mvc_get_version();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern Result mvc_engine_create(ref Config config, out EngineSafeHandle engine);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern void mvc_engine_destroy(IntPtr engine);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern Result mvc_engine_start(EngineSafeHandle engine);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern void mvc_engine_stop(EngineSafeHandle engine);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern Result mvc_engine_submit_capture_f32(
            EngineSafeHandle engine,
            [In] float[] samples,
            uint frameCount);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern Result mvc_engine_submit_render_f32(
            EngineSafeHandle engine,
            [In] float[] samples,
            uint frameCount,
            int delayMs);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern Result mvc_engine_pull_packet(
            EngineSafeHandle engine,
            [Out] byte[] bytes,
            uint capacity,
            out Packet packet);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern Result mvc_engine_add_voice(EngineSafeHandle engine, out uint voiceId);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern void mvc_engine_remove_voice(EngineSafeHandle engine, uint voiceId);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern Result mvc_engine_submit_packet(
            EngineSafeHandle engine,
            uint voiceId,
            [In] byte[] bytes,
            uint size,
            ushort sequence,
            uint timestamp);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern Result mvc_engine_pull_voice_f32(
            EngineSafeHandle engine,
            uint voiceId,
            [In, Out] float[] spatializationMask,
            uint frameCount);
    }
}
