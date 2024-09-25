// This is a derivative work of the following projects, which were created by Vatsal Ambastha:
// https://github.com/adrenak/univoice
// https://github.com/adrenak/univoice-unimic-input
// https://github.com/adrenak/unimic
// https://github.com/adrenak/univoice-audiosource-output

// This was created by Connor Myers (Metater):
// https://github.com/Metater

// Known issues:
// 1:
// High contigious packet loss and/or a momentary high jitter causes
// Need to catch up (squeaky voice) or slowdown because latency passed zero then went to the clip length
// Solutions:
// 1: Pause audio source until latency passes zero going from large number limit to zero.
// Probably want to prevent exact overlap of playing time and writing time, dont want to set a segment that is currently being played
// Mirror does this:
/*
    [Tooltip("Local timeline acceleration in % while catching up.")]
    [Range(0, 1)]
    public double catchupSpeed = 0.02f; // see snap interp demo. 1% is too slow.

    [Tooltip("Local timeline slowdown in % while slowing down.")]
    [Range(0, 1)]
    public double slowdownSpeed = 0.04f; // slow down a little faster so we don't encounter empty buffer (= jitter)
*/

// Use a timer for MetaVcs with output enabled to request the time from the player that is speaking to this MetaVc output
// Use the sent back time and the current time to calculate instantaneous jitter
// Perhaps use Exponential moving average on the jitter or just average it over the past second
// Use this jitter value as the latency target for the output audio source
// However, a minimum jitter value should be set that equals the target 5 segments of latency
// This 5 value could be lowered, but start with it. 5 * 25ms = 125ms
// Also, change the config output segment range to a plus or minus value. Just use plus or minus one segment, +- 25ms???
// Then set pitch equal to one while in this range, else use P controller to adjust

// If this becomes popular you could make a version where you implement MetaVc for your target networking library and don't depend on Mirror
// Support NGO?

// Post on reddit and mirror discord to advertise

// TODO FIX ISSUE WHERE YOU CANT SEND MORE THAN ONE PACKET PER FRAME, THIS IS VERY BAD, < 50 FPS MEANS TERRIBLE QUALITY

// MirrorVcManager could be created to handle all of the networking in a single place and to batch frames with the same timestamp sending from the server to clients,
// however this is extra complexity

// TODO Test with low video frame rates

// TODO Determine the minimum follow latency

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
        public MetaSerializableReactiveProperty<bool> isEchoEnabled;
        [Tooltip("This is the local player and they don't want to hear anyone else.")]
        public MetaSerializableReactiveProperty<bool> isDeafened;
        [Tooltip("This is the local player and they don't want anyone to hear them.")]
        public MetaSerializableReactiveProperty<bool> isInputMuted;
        [Tooltip("This is a remote player that the local player doesn't want to hear.")]
        public MetaSerializableReactiveProperty<bool> isOutputMuted;
        [Tooltip("This is the local player and they are speaking.")]
        public MetaSerializableReactiveProperty<bool> isSpeaking;

        private INetProvider netProvider;

        private bool isLocalPlayer;

        private VcEncoder encoder;
        private VcDecoder decoder;

        private VcJitter jitter;

        private readonly System.Diagnostics.Stopwatch stopwatch = new();
        private double Timestamp => stopwatch.Elapsed.TotalSeconds;

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

            stopwatch.Restart();
        }

        private void SendFrame(int index, float[] samples)
        {
            //if (samples != null)
            //{
            //    for (int i = 0; i < samples.Length; i++)
            //    {
            //        samples[i] = 0.2f * Mathf.Sin(Mathf.PI * i * (1.0f / 40.0f));
            //    }
            //}

            bool isSpeaking = samples != null;

            this.isSpeaking.Value = isSpeaking;

            bool shouldRelayEmpty = !isSpeaking || isDeafened || isInputMuted;
            if (shouldRelayEmpty)
            {
                if (isEchoEnabled)
                {
                    ReceiveFrame(index, Timestamp, ReadOnlySpan<byte>.Empty);
                }

                netProvider.RelayFrame(index, Timestamp, ReadOnlySpan<byte>.Empty);
            }
            else
            {
                var data = encoder.EncodeFrame(samples.AsSpan());

                if (isEchoEnabled)
                {
                    ReceiveFrame(index, Timestamp, data);
                }

                netProvider.RelayFrame(index, Timestamp, data);
            }
        }

        public void ReceiveFrame(int index, double timestamp, ReadOnlySpan<byte> data)
        {
            // TODO Use exponential backoff. Is the jitter stuff even needed?

            float targetLatency;
            if (isLocalPlayer)
            {
                // TODO Make this constant a variable?
                targetLatency = 0.027f;
            }
            else
            {
                float jitter = this.jitter.Update(timestamp);
                targetLatency = Mathf.Max(jitter, 0.032f); // 40ms seemed to work, 27 ms occasional pauses
            }

            if (data.Length == 0)
            {
                SetIsSpeaking(false);

                audioOutput.ReceiveFrame(index, null, targetLatency);
            }
            else
            {
                SetIsSpeaking(true);

                if (netProvider.IsLocalPlayerDeafened || isOutputMuted)
                {
                    audioOutput.ReceiveFrame(index, null, targetLatency);
                }
                else
                {
                    var samples = decoder.DecodeFrame(data);
                    if (samples.Length == config.samplesPerFrame)
                    {
                        var array = FixedLengthArrayPool<float>.Rent(samples.Length);
                        samples.CopyTo(array);
                        audioOutput.ReceiveFrame(index, array, targetLatency);
                        FixedLengthArrayPool<float>.Return(array);
                    }
                    else
                    {
                        // Silently ignore the frame with invalid length
                        audioOutput.ReceiveFrame(index, null, targetLatency);
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
            if (isEchoEnabled && isLocalPlayer)
            {
                // Return because isSpeaking is already set in SendFrame
                return;
            }

            isSpeaking.Value = value;
        }
    }
}