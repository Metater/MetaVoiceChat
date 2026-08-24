#if UNITY_EDITOR && META_VOICE_CHAT_TESTS
using MetaVoiceChat.NetEq;
using MetaVoiceChat.Utils;
using NUnit.Framework;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using UnityEngine;

namespace MetaVoiceChat.Tests.Editor
{
    public class NetEqInteropTests
    {
        private const string TrustedDllSha256 = "2881159D2359DE92F2EDAC78DF0ADAF19AF57148E2575A1BF31628700C77F54F";

        [Test]
        public void TrustedNativeDll_HasExpectedSha256()
        {
            string path = Path.Combine(
                Application.dataPath,
                "Metater",
                "MetaVoiceChat",
                "NetEQ",
                "meta_voice_chat_neteq.dll");

            Assert.That(File.Exists(path), Is.True, $"Trusted NetEQ DLL was not found at {path}.");
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            string actual = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);

            Assert.That(actual, Is.EqualTo(TrustedDllSha256));
        }

        [Test]
        public void UnmanagedFloatArray_RoundTripsResizesZerosAndFreesIdempotently()
        {
            UnmanagedFloatArray buffer = new UnmanagedFloatArray();
            try
            {
                IntPtr pointer = buffer.GetOrInit(4, out float[] managed, initToZero: false);
                Assert.That(pointer, Is.Not.EqualTo(IntPtr.Zero));

                managed[0] = 1.25f;
                managed[1] = -2.5f;
                managed[2] = 3.75f;
                managed[3] = -4f;
                buffer.WriteToUnmanaged();

                float[] copied = new float[4];
                Marshal.Copy(pointer, copied, 0, copied.Length);
                Assert.That(copied, Is.EqualTo(managed));

                float[] nativeValues = { 9f, 8f, 7f, 6f };
                Marshal.Copy(nativeValues, 0, pointer, nativeValues.Length);
                buffer.ReadFromUnmanaged();
                Assert.That(managed, Is.EqualTo(nativeValues));

                float[] source = { 100f, 10f, 20f, 30f, 200f };
                buffer.Fill(new ArraySegment<float>(source, 1, 3));
                IntPtr resizedPointer = buffer.GetOrInit(3, out float[] resizedManaged, initToZero: false);
                Assert.That(resizedPointer, Is.Not.EqualTo(IntPtr.Zero));
                Assert.That(resizedManaged, Is.EqualTo(new[] { 10f, 20f, 30f }));

                buffer.Zero();
                Assert.That(resizedManaged, Is.EqualTo(new[] { 0f, 0f, 0f }));
            }
            finally
            {
                buffer.Free();
                buffer.Free();
            }
        }

        [Test]
        public void TrustedNativeAbi_CreatesInsertsReadsAndFreesMonoAudio()
        {
            RequireWindowsX64();

            const int sampleRate = 48000;
            const int packetSamples = 960;
            const int readSamplesCapacity = 480;
            IntPtr netEq = IntPtr.Zero;
            UnmanagedFloatArray packetBuffer = new UnmanagedFloatArray();
            UnmanagedFloatArray readBuffer = new UnmanagedFloatArray();

            try
            {
                netEq = NetEqInterop.CreateNetEq(
                    sampleRate,
                    1,
                    50,
                    100,
                    20,
                    0);
                Assert.That(netEq, Is.Not.EqualTo(IntPtr.Zero));

                float[] packet = new float[packetSamples];
                for (int packetIndex = 0; packetIndex < 10; packetIndex++)
                {
                    int sampleOffset = packetIndex * packetSamples;
                    for (int i = 0; i < packet.Length; i++)
                    {
                        packet[i] = 0.2f * Mathf.Sin(2f * Mathf.PI * 440f * (sampleOffset + i) / sampleRate);
                    }

                    IntPtr packetPointer = packetBuffer.GetOrInit(packetSamples, out _, initToZero: false);
                    packetBuffer.Fill(new ArraySegment<float>(packet));
                    NetEqInterop.InsertPacket(
                        netEq,
                        (ushort)packetIndex,
                        (uint)sampleOffset,
                        packetPointer,
                        packetSamples,
                        sampleRate,
                        1,
                        20);
                }

                IntPtr readPointer = readBuffer.GetOrInit(readSamplesCapacity, out float[] samples, initToZero: false);
                int totalRead = 0;
                for (int i = 0; i < 20; i++)
                {
                    int read = NetEqInterop.GetAudio(netEq, readPointer, readSamplesCapacity);
                    Assert.That(read, Is.InRange(0, readSamplesCapacity));
                    if (read == 0)
                    {
                        continue;
                    }

                    readBuffer.ReadFromUnmanaged(read);
                    totalRead += read;
                    for (int sampleIndex = 0; sampleIndex < read; sampleIndex++)
                    {
                        Assert.That(
                            !float.IsNaN(samples[sampleIndex]) && !float.IsInfinity(samples[sampleIndex]),
                            Is.True);
                    }
                }

                Assert.That(totalRead, Is.GreaterThan(0));
                Assert.That(NetEqInterop.CurrentBufferSizeMs(netEq), Is.LessThanOrEqualTo(1000));
            }
            finally
            {
                packetBuffer.Free();
                readBuffer.Free();
                if (netEq != IntPtr.Zero)
                {
                    NetEqInterop.FreeNetEq(netEq);
                }
            }
        }

        private static void RequireWindowsX64()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor || IntPtr.Size != 8)
            {
                Assert.Ignore("The trusted NetEQ plugin is available only in the Windows x64 editor.");
            }
        }
    }
}
#endif
