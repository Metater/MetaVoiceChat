// Based on: https://github.com/adrenak/univoice-audiosource-output/blob/master/Assets/Adrenak.UniVoice.AudioSourceOutput/Runtime/CircularAudioClip.cs

using System;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.Output
{
    public class VcAudioClip : IDisposable
    {
        private readonly VcConfig config;

        private readonly AudioClip audioClip;

        private readonly float[] emptyFrame;
        private readonly float[] emptyClip;

        public float Length => audioClip.length;

        public VcAudioClip(VcConfig config)
        {
            this.config = config;

            audioClip = AudioClip.Create(nameof(VcAudioClip), config.OutputSampleCount, VcConfig.ChannelCount, VcConfig.SamplesPerSecond, false);

            emptyFrame = new float[config.general.samplesPerFrame];
            emptyClip = new float[config.OutputSampleCount];

            var audioSource = config.general.outputAudioSource;
            audioSource.loop = true;
            audioSource.clip = audioClip;
        }

        public void WriteFrame(int offsetFrames, float[] frame)
        {
            frame ??= emptyFrame;

            if (frame.Length != config.general.samplesPerFrame)
            {
                throw new Exception("Voice chat audio clip frame length does not match the config!");
            }

            audioClip.SetData(frame, config.general.samplesPerFrame * offsetFrames);
        }

        public int GetOffsetFrames(int frameIndex)
        {
            return frameIndex % config.OutputSegmentCount;
        }

        public void ClearFrame(int offsetFrames)
        {
            audioClip.SetData(emptyFrame, config.general.samplesPerFrame * offsetFrames);
        }

        public void Clear()
        {
            audioClip.SetData(emptyClip, 0);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(audioClip);
        }
    }
}