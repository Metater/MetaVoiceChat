#define ECHO
// ^^^ Use this to echo the local voice

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

using Assets.Metater.MetaVoiceChat.General;
using Assets.Metater.MetaVoiceChat.Input;
using Assets.Metater.MetaVoiceChat.Output;
using Assets.Metater.MetaVoiceChat.VcImpls;
using Mirror;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat
{
    public class MetaVc : MonoBehaviour
    {
        public VcConfig config;

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

        public IVcImpl Impl { get; set; }

        public bool IsLocalPlayer { get; private set; }

        public AudioListener AudioListener { get; private set; }

        public VcEncoder Encoder { get; private set; }
        public VcDecoder Decoder { get; private set; }

        public VcLocalJitter LocalJitter { get; private set; }
        public VcRemoteJitter RemoteJitter { get; private set; }

        public VcAudioInput AudioInput { get; private set; }
        public VcAudioOutput AudioOutput { get; private set; }

        public void StartClient(IVcImpl impl, bool isLocalPlayer, int maxDataBytesPerPacket)
        {
            config.general.Cache(config, this);
            config.general.outputAudioSource.dopplerLevel = 0;

            Impl = impl;

            IsLocalPlayer = isLocalPlayer;

            AudioListener = FindObjectOfType<AudioListener>();

            if (isLocalPlayer)
            {
                Encoder = new(config, maxDataBytesPerPacket);

                LocalJitter = new();

                AudioInput = new(config);
                AudioInput.OnFrameReady += SendFrame;
            }
#if !ECHO
            else
#endif
            {
                Decoder = new(config);

                // TODO Make settings configurable
                RemoteJitter = new(1, 0);

                AudioOutput = new(config);
            }
        }

        private void SendFrame(int segmentIndex, float[] segment)
        {
            //if (segment != null)
            //{
            //    for (int i = 0; i < segment.Length; i++)
            //    {
            //        segment[i] = 0.2f * Mathf.Sin(Mathf.PI * i * (1.0f / 40.0f));
            //    }
            //}

            bool isSpeaking = segment != null;

            IsSpeaking = isSpeaking;

            bool shouldSendEmpty = !isSpeaking || IsDeafened || IsInputMuted;
            if (shouldSendEmpty)
            {
                CmdRelayEmptyAudioSegment(segmentIndex, LocalJitter.TimeSinceStart);
            }
            else
            {
                //if (Random.value < 0.05f)
                //{
                //    return;
                //}

                // TODO Dont send audio to players that are too far away. Maybe send this, but go inside of the CmdRelay place and relay and empty segment
                // instead if out of range, use audio listener reference and position of mouth.
                CmdRelayAudioSegment(segmentIndex, new(netId, segmentIndex, segment), LocalJitter);
            }

            //short[] frame = new short[segment.Length];
            //for (int i = 0; i < segment.Length; i++)
            //{
            //    frame[i] = (short)(short.MaxValue * segment[i]);
            //}

            //var encodedData = Encoder.Encode(frame);
            //var decodedData = Decoder.Decode(encodedData);

            //print($"{encodedData.Length} {decodedData.Length}");
        }

        [Command(channel = Channels.Unreliable)]
        private void CmdRelayAudioSegment(int segmentIndex, VcSegment segment, double time)
        {
            RpcReceiveAudioSegment(segmentIndex, segment, time);
        }

        [Command(channel = Channels.Unreliable)]
        private void CmdRelayEmptyAudioSegment(int segmentIndex, double time)
        {
            RpcReceiveEmptyAudioSegment(segmentIndex, time);
        }

#if ECHO
        [ClientRpc(channel = Channels.Unreliable, includeOwner = true)]
#else
        [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
#endif
        private void RpcReceiveAudioSegment(int segmentIndex, VcSegment segment, double time)
        {
            SetIsSpeaking(true);
            if (Impl.IsLocalPlayerDeafened || IsOutputMuted)
            {
                AudioOutput.FeedSegment(segmentIndex);
            }
            else
            {
                AudioOutput.FeedSegment(segmentIndex, segment.UncompressedBuffer);
            }

            //float jitter = RemoteJitter.Update(time);
        }

#if ECHO
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

        public void StopClient(bool isLocalPlayer)
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
#if ECHO
            if (!IsLocalPlayer)
#endif
                IsSpeaking = value;
        }
    }
}