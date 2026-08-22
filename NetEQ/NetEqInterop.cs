using System;
using System.Runtime.InteropServices;

namespace MetaVoiceChat.NetEq
{
    public static class NetEqInterop
    {
        private const string DllName =
        //#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        //            "libmeta_voice_chat_neteq.so";
        //#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        //            "libmeta_voice_chat_neteq.dylib";
        //#else
            "meta_voice_chat_neteq.dll";
        //#endif

        /// <summary>
        /// Creates a new NetEq instance with the specified configuration.
        /// </summary>
        /// <returns>Pointer to the NetEq instance, or null on failure.</returns>
        [DllImport(DllName, EntryPoint = "create_neteq", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateNetEq(
            uint sampleRate,
            byte channels,
            int maxPacketsInBuffer,
            uint maxDelayMs,
            uint minDelayMs,
            uint additionalDelayMs);

        /// <summary>
        /// Frees a NetEq instance.
        /// </summary>
        [DllImport(DllName, EntryPoint = "free_neteq", CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeNetEq(IntPtr ptr);

        /// <summary>
        /// Inserts an audio packet into the NetEq buffer.
        /// </summary>
        [DllImport(DllName, EntryPoint = "insert_packet", CallingConvention = CallingConvention.Cdecl)]
        public static extern void InsertPacket(
            IntPtr ptr,
            ushort sequenceNumber,
            uint timestamp,
            IntPtr samples,
            int samplesLength,
            uint sampleRate,
            byte channels,
            uint durationMs);

        /// <summary>
        /// Gets audio samples from NetEq.
        /// </summary>
        /// <returns>Number of samples retrieved.</returns>
        [DllImport(DllName, EntryPoint = "get_audio", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetAudio(IntPtr ptr, IntPtr samples, int samplesLength);

        /// <summary>
        /// Gets the current buffer size in milliseconds.
        /// </summary>
        /// <returns>Current buffer size in ms.</returns>
        [DllImport(DllName, EntryPoint = "current_buffer_size_ms", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint CurrentBufferSizeMs(IntPtr ptr);
    }
}