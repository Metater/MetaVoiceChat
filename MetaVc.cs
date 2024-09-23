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

// Missing functionality:
// 1:
// Spacial audio controls, i.e. muting of a player's mouth after a certain distance
// 2:
// Audio encoding, probably Opus
// 3:
// Support for only single channel microphones.
// Unity's Microphone class may be limited to only one, not sure

// TODO Spacial cuttoff, use logorithmic dropoff, send empty segments when out of range
// ^^^^^^ JUST USE THE OUTPUT AUDIO SOURCE 3D MAX DISTANCE

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

using System;
using Assets.Metater.MetaVoiceChat.General;
using Assets.Metater.MetaVoiceChat.Input;
using Assets.Metater.MetaVoiceChat.NetProviders;
using Assets.Metater.MetaVoiceChat.Output;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat
{
    public class MetaVc : MonoBehaviour
    {
        public VcConfig config;

        /// <summary>
        /// This plays back the voice of the local player
        /// </summary>
        public MetaSerializableReactiveProperty<bool> isEchoEnabled;
        /// <summary>
        /// This is the local player and they don't want to hear anyone else
        /// </summary>
        public MetaSerializableReactiveProperty<bool> isDeafened;
        /// <summary>
        /// This is the local player and they don't want anyone to hear them
        /// </summary>
        public MetaSerializableReactiveProperty<bool> isInputMuted;
        /// <summary>
        /// This is a remote player that the local player doesn't want to hear
        /// </summary>
        public MetaSerializableReactiveProperty<bool> isOutputMuted;
        /// <summary>
        /// This is the local player and they are speaking
        /// </summary>
        public MetaSerializableReactiveProperty<bool> isSpeaking;

        private INetProvider netProvider;

        private bool isLocalPlayer;

        private VcEncoder encoder;
        private VcDecoder decoder;

        private VcLocalJitter localJitter;
        private VcRemoteJitter remoteJitter;

        private VcAudioInput audioInput;
        private VcAudioOutput audioOutput;

        public void StartClient(INetProvider netProvider, bool isLocalPlayer, int maxDataBytesPerPacket)
        {
            config.general.Init(config, this);

            this.netProvider = netProvider;

            this.isLocalPlayer = isLocalPlayer;

            if (isLocalPlayer)
            {
                encoder = new(config, maxDataBytesPerPacket);

                localJitter = new();

                audioInput = new(config);
                audioInput.OnFrameReady += SendFrame;
            }

            decoder = new(config);

            // TODO Make settings configurable, also 1 second window is too big, maybe 100ms
            remoteJitter = new(1, 0);

            audioOutput = new(config);
        }

        private void SendFrame(int index, float[] samples)
        {
            //if (segment != null)
            //{
            //    for (int i = 0; i < segment.Length; i++)
            //    {
            //        segment[i] = 0.2f * Mathf.Sin(Mathf.PI * i * (1.0f / 40.0f));
            //    }
            //}

            bool isSpeaking = samples != null;

            this.isSpeaking.Value = isSpeaking;

            bool shouldRelayEmpty = !isSpeaking || isDeafened || isInputMuted;
            if (shouldRelayEmpty)
            {
                var timestamp = localJitter.Timestamp;

                if (isEchoEnabled)
                {
                    ReceiveFrame(index, timestamp, ReadOnlySpan<byte>.Empty);
                }

                netProvider.RelayFrame(index, timestamp, ReadOnlySpan<byte>.Empty);
            }
            else
            {
                var timestamp = localJitter.Timestamp;
                var data = encoder.EncodeFrame(samples.AsSpan());

                if (isEchoEnabled)
                {
                    ReceiveFrame(index, timestamp, data);
                }

                netProvider.RelayFrame(index, localJitter.Timestamp, data);
            }
        }

        public void ReceiveFrame(int index, double timestamp, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                SetIsSpeaking(false);
                audioOutput.FeedFrame(index);

                //float jitter = RemoteJitter.Update(time);
            }
            else
            {
                SetIsSpeaking(true);
                if (netProvider.IsLocalPlayerDeafened || isOutputMuted)
                {
                    audioOutput.FeedFrame(index);
                }
                else
                {
                    var samples = decoder.DecodeFrame(data);
                    var array = FixedLengthArrayPool<float>.Rent(samples.Length);
                    samples.CopyTo(array);
                    audioOutput.FeedFrame(index, array);
                    FixedLengthArrayPool<float>.Return(array);
                }

                //float jitter = RemoteJitter.Update(time);
            }
        }

        public void StopClient()
        {
            if (isLocalPlayer)
            {
                encoder.Dispose();

                audioInput.OnFrameReady -= SendFrame;
                audioInput.Dispose();
            }

            decoder.Dispose();

            audioOutput.Dispose();
        }

        private void SetIsSpeaking(bool value)
        {
            if (isEchoEnabled && isLocalPlayer)
            {
                return;
            }

            isSpeaking.Value = value;
        }
    }
}