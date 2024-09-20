#define ECHO
// ^^^ Use this to echo the local voice.

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

using System.Diagnostics;
using System.Text;
using Assets.Metater.MetaRefs;
using Assets.Metater.MetaVoiceChat.Input;
using Assets.Metater.MetaVoiceChat.Output;
using Mirror;
using R3;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat
{
    public class MetaVc : MetaNbl<MetaVc>
    {
        [Header("General")]
        public VcConfig config;

        [Header("Values")]
        /// <summary>
        /// This is the local player and they don't want to hear anyone else
        /// </summary>
        public SerializableReactiveProperty<bool> isDeafened;
        /// <summary>
        /// This is the local player and they don't want anyone to hear them
        /// </summary>
        public SerializableReactiveProperty<bool> isInputMuted;
        /// <summary>
        /// This is a remote player that the local player doesn't want to hear
        /// </summary>
        public SerializableReactiveProperty<bool> isOutputMuted;
        /// <summary>
        /// This is the local player and they are speaking
        /// </summary>
        public SerializableReactiveProperty<bool> isSpeaking;

        private VcAudioInput audioInput;
        private VcAudioOutput audioOutput;

        private VcLocalJitter localJitter;
        private VcRemoteJitter remoteJitter;

        private AudioListener audioListener;

        private readonly StringBuilder csv = new();

        private void Awake()
        {
            config.CoroutineProvider = this;
            config.OutputAudioSource.dopplerLevel = 0;
            VcSegment.Registry.SetSamplesPerSegment(config.SamplesPerSegment);

            csv.AppendLine("Time,Jitter");
            //csv.AppendLine("Time,Diff");
            //csv.AppendLine("Time,dt");

            audioListener = FindObjectOfType<AudioListener>();
        }

        protected override void MetaOnStartClient()
        {
            if (isLocalPlayer)
            {
                audioInput = new(config);
                audioInput.OnSegmentReady += SendAudioSegment;

                localJitter = new();
            }
#if !ECHO
            else
#endif
            {
                audioOutput = new(config);

                remoteJitter = new(1, 0);
            }
        }

        Stopwatch test = new Stopwatch();

        private void SendAudioSegment(int segmentIndex, float[] segment)
        {
            //if (segment != null)
            //{
            //    for (int i = 0; i < segment.Length; i++)
            //    {
            //        segment[i] = 0.2f * Mathf.Sin(Mathf.PI * i * (1.0f / 40.0f));
            //    }
            //}

            bool isSpeaking = segment != null;

            this.isSpeaking.Value = isSpeaking;

            bool shouldSendEmpty = !isSpeaking || isDeafened.Value || isInputMuted.Value;
            if (shouldSendEmpty)
            {
                CmdRelayEmptyAudioSegment(segmentIndex, localJitter.TimeSinceStart);
            }
            else
            {
                //if (Random.value < 0.05f)
                //{
                //    return;
                //}

                // TODO Dont send audio to players that are too far away. Maybe send this, but go inside of the CmdRelay place and relay and empty segment
                // instead if out of range, use audio listener reference and position of mouth.
                CmdRelayAudioSegment(segmentIndex, new(netId, segmentIndex, segment), localJitter.TimeSinceStart);
            }

            //csv.AppendLine(Time.time + "," + test.Elapsed.TotalSeconds);
            //csv.AppendLine(Time.time + "," + Time.deltaTime);
            test.Restart();
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
            if (LocalPlayerInstance.isDeafened.Value || isOutputMuted.Value)
            {
                audioOutput.FeedSegment(segmentIndex);
            }
            else
            {
                audioOutput.FeedSegment(segmentIndex, segment.UncompressedBuffer);
            }

            float jitter = remoteJitter.Update(time);
            csv.AppendLine(Time.time + "," + jitter);
        }

#if ECHO
        [ClientRpc(channel = Channels.Unreliable, includeOwner = true)]
#else
        [ClientRpc(channel = Channels.Unreliable, includeOwner = false)]
#endif
        private void RpcReceiveEmptyAudioSegment(int segmentIndex, double time)
        {
            SetIsSpeaking(false);
            audioOutput.FeedSegment(segmentIndex);

            float jitter = remoteJitter.Update(time);
            csv.AppendLine(Time.time + "," + jitter);
        }

        protected override void MetaOnStopClient()
        {
            if (audioInput != null)
            {
                audioInput.OnSegmentReady -= SendAudioSegment;
                audioInput.Dispose();
            }

            audioOutput?.Dispose();

            print(csv.ToString());
        }

        private void SetIsSpeaking(bool value)
        {
#if ECHO
            if (!isLocalPlayer)
#endif
                isSpeaking.Value = value;
        }
    }
}