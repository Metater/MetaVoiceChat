// This project referenced the following projects in its beginnings, which were created by Vatsal Ambastha:
// https://github.com/adrenak/univoice
// https://github.com/adrenak/univoice-unimic-input
// https://github.com/adrenak/unimic
// https://github.com/adrenak/univoice-audiosource-output

// This was created by Connor Myers (Metater):
// https://github.com/Metater

using System;
using Assets.Metater.MetaVoiceChat.Input;
using Assets.Metater.MetaVoiceChat.NetProviders;
using Assets.Metater.MetaVoiceChat.Opus;
using Assets.Metater.MetaVoiceChat.Output;
using Assets.Metater.MetaVoiceChat.Utils;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat
{
    public class MetaVc : MonoBehaviour
    {
        public VcAudioInput audioInput;
        public VcAudioOutput audioOutput;
        public VcConfig config;

        [Tooltip("This plays back the voice of the local player.")]
        public bool isEchoEnabled;

        [Tooltip("This overwrites the audio input with a 200 Hz sine wave at 20% volume.")]
        public bool isSineEnabled;

        [Tooltip("This is the local player and they don't want to hear anyone else.")]
        public MetaSerializableReactiveProperty<bool> isDeafened;
        [Tooltip("This is the local player and they don't want anyone to hear them.")]
        public MetaSerializableReactiveProperty<bool> isInputMuted;
        [Tooltip("This is a remote player that the local player doesn't want to hear.")]
        public MetaSerializableReactiveProperty<bool> isOutputMuted;
        [Tooltip("This player is speaking or trying to speak.")]
        public MetaSerializableReactiveProperty<bool> isSpeaking;

        private INetProvider netProvider;

        private bool isLocalPlayer;

        private VcEncoder encoder;
        private VcDecoder decoder;

        private VcJitter jitter;

        private readonly System.Diagnostics.Stopwatch stopwatch = new();
        private double Timestamp => stopwatch.Elapsed.TotalSeconds;

        private bool CannotSpeak => netProvider.IsLocalPlayerDeafened || isOutputMuted;

        private void Awake()
        {
            config.Init();
        }

        public void StartClient(INetProvider netProvider, bool isLocalPlayer, int maxDataBytesPerPacket)
        {
            this.netProvider = netProvider;

            this.isLocalPlayer = isLocalPlayer;

            if (isLocalPlayer)
            {
                encoder = new(config, maxDataBytesPerPacket);

                audioInput.OnFrameReady += SendFrame;
                audioInput.StartLocalPlayer();
            }

            decoder = new(config);

            jitter = new(config);

            stopwatch.Start();
        }

        private void SendFrame(int index, float[] samples)
        {
            if (samples != null && isSineEnabled)
            {
                const float Amplitude = 0.2f;

                float multiplier = Mathf.PI * (1.0f / 40.0f); // 200 Hz

                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = Amplitude * Mathf.Sin(i * multiplier);
                }
            }

            bool isSpeaking = samples != null;

            this.isSpeaking.Value = isSpeaking;

            bool shouldRelayEmpty = !isSpeaking || isDeafened || isInputMuted;
            if (shouldRelayEmpty)
            {
                if (isEchoEnabled)
                {
                    ReceiveFrame(index, Timestamp, additionalLatency: 0, ReadOnlySpan<byte>.Empty);
                }

                netProvider.RelayFrame(index, Timestamp, ReadOnlySpan<byte>.Empty);
            }
            else
            {
                var data = encoder.EncodeFrame(samples.AsSpan());

                if (isEchoEnabled)
                {
                    ReceiveFrame(index, Timestamp, additionalLatency: 0, data);
                }

                netProvider.RelayFrame(index, Timestamp, data);
            }
        }

        public void ReceiveFrame(int index, double timestamp, float additionalLatency, ReadOnlySpan<byte> data)
        {
            float targetLatency = (config.secondsPerFrame * config.outputMinBufferSize) + Time.deltaTime + additionalLatency;

            if (!isLocalPlayer)
            {
                // Exponential backoff is an alternative to the current jitter calculation, but this works!
                float jitter = this.jitter.Update(timestamp);
                targetLatency += jitter;
            }

            if (data.Length == 0)
            {
                SetIsSpeaking(false);

                audioOutput.ReceiveAndFilterFrame(index, null, targetLatency);
            }
            else
            {
                SetIsSpeaking(true);

                if (CannotSpeak)
                {
                    audioOutput.ReceiveAndFilterFrame(index, null, targetLatency);
                }
                else
                {
                    var samples = decoder.DecodeFrame(data);

                    if (samples.Length == config.samplesPerFrame)
                    {
                        var array = FixedLengthArrayPool<float>.Rent(samples.Length);
                        samples.CopyTo(array);
                        audioOutput.ReceiveAndFilterFrame(index, array, targetLatency);
                        FixedLengthArrayPool<float>.Return(array);
                    }
                    else
                    {
                        // Silently ignore the frame with invalid length
                        audioOutput.ReceiveAndFilterFrame(index, null, targetLatency);
                    }
                }
            }
        }

        public void StopClient()
        {
            if (isLocalPlayer)
            {
                encoder.Dispose();

                audioInput.OnFrameReady -= SendFrame;
            }

            decoder.Dispose();
        }

        private void SetIsSpeaking(bool value)
        {
            if (isLocalPlayer/* && isEchoEnabled*/)
            {
                // If this is a local player, the only way to get here is echo being enabled
                // Return because isSpeaking is already set in SendFrame for echo mode
                return;
            }

            isSpeaking.Value = value;
        }
    }
}