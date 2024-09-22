// This is a derivative work of the following projects, which were created by Vatsal Ambastha:
// https://github.com/adrenak/univoice
// https://github.com/adrenak/univoice-unimic-input
// https://github.com/adrenak/unimic
// https://github.com/adrenak/univoice-audiosource-output

// This was created by Connor Myers (Metater):
// https://github.com/Metater

// Take the following steps to adapt this to your own project:
// 1. Ensure you have the MetaRefs project accessible to this.
// 2. Ensure you have the MetaUtils project accessible to this.

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

// Make a standalone version that doesn't have any dependencies
// But, have R3MetaVc and R3VcAudioProcessor with SerializableReactiveProperties

// If this becomes popular you could make a version where you implement MetaVc for your target networking library and don't depend on Mirror
// Support NGO?

// Post on reddit and mirror discord to advertise

// TODO You might want to switch from 25ms segments to 20ms segments to better support opus

// TODO FIX ISSUE WHERE YOU CANT SEND MORE THAN ONE PACKET PER FRAME, THIS IS VERY BAD, < 50 FPS MEANS TERRIBLE QUALITY

// MirrorVcManager could be created to handle all of the networking in a single place and to batch frames with the same timestamp sending from the server to clients,
// however this is extra complexity

using System;
using Assets.Metater.MetaVoiceChat.General;
using Assets.Metater.MetaVoiceChat.Input;
using Assets.Metater.MetaVoiceChat.NetProviders;
using Assets.Metater.MetaVoiceChat.Output;
using Mirror;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat
{
    public class MetaVc : MonoBehaviour
    {
        public VcConfig config;

        /// <summary>
        /// This plays back the voice of the local player
        /// </summary>
        public bool IsEchoEnabled { get; set; }
        /// <summary>
        /// This is the local player and they don't want to hear anyone else
        /// </summary>
        public bool IsDeafened { get; set; }
        /// <summary>
        /// This is the local player and they don't want anyone to hear them
        /// </summary>
        public bool IsInputMuted { get; set; }
        /// <summary>
        /// This is a remote player that the local player doesn't want to hear
        /// </summary>
        public bool IsOutputMuted { get; set; }
        /// <summary>
        /// This is the local player and they are speaking
        /// </summary>
        public bool IsSpeaking { get; set; }

        public INetProvider VcImpl { get; set; }

        public bool IsLocalPlayer { get; private set; }

        public AudioListener AudioListener { get; private set; }

        public VcEncoder Encoder { get; private set; }
        public VcDecoder Decoder { get; private set; }

        public VcLocalJitter LocalJitter { get; private set; }
        public VcRemoteJitter RemoteJitter { get; private set; }

        public VcAudioInput AudioInput { get; private set; }
        public VcAudioOutput AudioOutput { get; private set; }

        public void StartClient(INetProvider vcImpl, bool isLocalPlayer, int maxDataBytesPerPacket)
        {
            config.general.Cache(config, this);
            config.general.outputAudioSource.dopplerLevel = 0;

            VcImpl = vcImpl;

            IsLocalPlayer = isLocalPlayer;

            AudioListener = FindObjectOfType<AudioListener>();

            if (isLocalPlayer)
            {
                Encoder = new(config, maxDataBytesPerPacket);

                LocalJitter = new();

                AudioInput = new(config);
                AudioInput.OnFrameReady += SendFrame;
            }

            Decoder = new(config);

            // TODO Make settings configurable, also 1 second window is too big, maybe 100ms
            RemoteJitter = new(1, 0);

            AudioOutput = new(config);
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

            IsSpeaking = isSpeaking;

            bool shouldRelayEmpty = !isSpeaking || IsDeafened || IsInputMuted;
            if (shouldRelayEmpty)
            {
                var timestamp = LocalJitter.TimeSinceStart;

                if (IsEchoEnabled)
                {
                    ReceiveFrame(index, timestamp, ReadOnlySpan<byte>.Empty);
                }

                VcImpl.RelayFrame(index, timestamp, ReadOnlySpan<byte>.Empty);
            }
            else
            {
                var timestamp = LocalJitter.TimeSinceStart;
                var data = Encoder.EncodeFrame(samples.AsSpan());

                if (IsEchoEnabled)
                {
                    ReceiveFrame(index, timestamp, data);
                }

                VcImpl.RelayFrame(index, LocalJitter.TimeSinceStart, data);
            }
        }

        public void ReceiveFrame(int index, double timestamp, ReadOnlySpan<byte> data)
        {
            var
        }

#if META_VOICE_CHAT_ECHO
        [ClientRpc(channel = Channels.Unreliable, includeOwner = true)]
#else
        [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
#endif
        private void RpcReceiveAudioSegment(int segmentIndex, VcSegment segment, double time)
        {
            SetIsSpeaking(true);
            if (VcImpl.IsLocalPlayerDeafened || IsOutputMuted)
            {
                AudioOutput.FeedSegment(segmentIndex);
            }
            else
            {
                AudioOutput.FeedSegment(segmentIndex, segment.UncompressedBuffer);
            }

            //float jitter = RemoteJitter.Update(time);
        }

#if META_VOICE_CHAT_ECHO
        [ClientRpc(channel = Channels.Unreliable, includeOwner = true)]
#else
        [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
#endif
        private void RpcReceiveEmptyAudioSegment(int segmentIndex, double time)
        {
            SetIsSpeaking(false);
            AudioOutput.FeedSegment(segmentIndex);

            //float jitter = RemoteJitter.Update(time);
        }

        public void StopClient()
        {
            if (AudioInput != null)
            {
                AudioInput.OnFrameReady -= SendFrame;
                AudioInput.Dispose();
            }

            AudioOutput?.Dispose();
        }

        private void SetIsSpeaking(bool value)
        {
#if META_VOICE_CHAT_ECHO
            if (!IsLocalPlayer)
#endif
            IsSpeaking = value;
        }
    }
}